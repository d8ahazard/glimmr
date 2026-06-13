#region

using System;
using System.Collections.Generic;
using System.Drawing;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Glimmr.Enums;
using Glimmr.Models.Data;
using Glimmr.Models.Frame;
using Glimmr.Models.Helper;
using Glimmr.Models.Util;
using Glimmr.Services;
using Makaretu.Dns;
using Serilog;

#endregion

namespace Glimmr.Models.ColorSource.HueSync;

/// <summary>
///     Color source that turns Glimmr into an emulated Hue Bridge. The Hue Sync TV app streams its
///     entertainment data to us over DTLS; we decode it (via <see cref="HueDtlsServer" /> and
///     <see cref="HueSyncBridge" />), map the per-channel colors onto Glimmr's sector grid, and push
///     the result through the standard FrameBuilder / FrameSplitter pipeline so every target lights up.
/// </summary>
public class HueSyncStream : ColorSource {
	public override bool SourceActive => _sourceActive;
	public sealed override FrameBuilder? Builder { get; set; }
	public sealed override FrameSplitter Splitter { get; set; }

	private readonly HueSyncBridge _bridge;
	private readonly ControlService _cs;
	private readonly HueDtlsServer _dtls;

	private DeviceMode _devMode;
	private int _hSectors;
	private int _vSectors;
	private int _sectorCount;
	private bool _sourceActive;
	private volatile bool _building;
	private CancellationTokenSource _disableSource;
	private Task? _disableTask;
	private ServiceDiscovery? _discovery;

	public HueSyncStream(ColorService cs) {
		_cs = cs.ControlService;
		_cs.RefreshSystemEvent += RefreshSystem;
		_cs.SetModeEvent += Mode;
		Splitter = new FrameSplitter(cs);
		_disableSource = new CancellationTokenSource();

		_bridge = HueSyncBridge.Instance;
		_bridge.FrameReceived += OnFrame;
		_bridge.StreamStateChanged += OnStreamStateChanged;

		RefreshSystem();

		_dtls = new HueDtlsServer(_bridge);
		_dtls.Start();
	}

	public override Task Start(CancellationToken ct) {
		Log.Information("Starting Hue Sync stream service...");
		Splitter.DoSend = true;
		RunTask = ExecuteAsync(ct);
		return Task.CompletedTask;
	}

	public sealed override void RefreshSystem() {
		var sd = DataUtil.GetSystemData();
		_devMode = sd.DeviceMode;
		_hSectors = sd.HSectors;
		_vSectors = sd.VSectors;
		_sectorCount = sd.SectorCount;
		// Build a sector-shaped frame: left/right = vertical sectors, top/bottom = horizontal sectors.
		var dims = new[] { _vSectors, _vSectors, _hSectors, _hSectors };
		Builder = new FrameBuilder(dims, true);
		AdvertiseBridge();
	}

	/// <summary>
	///     Advertise the emulated bridge over mDNS (_hue._tcp) so the Hue Sync app can discover it
	///     on the local network, mirroring how a real Hue Bridge announces itself.
	/// </summary>
	private void AdvertiseBridge() {
		try {
			_discovery?.Dispose();
			var addresses = new List<IPAddress> { IPAddress.Parse(IpUtil.GetLocalIpAddress()) };
			var service = new ServiceProfile(_bridge.BridgeId, "_hue._tcp", 80, addresses);
			service.AddProperty("bridgeid", _bridge.BridgeId);
			service.AddProperty("modelid", "BSB002");
			_discovery = new ServiceDiscovery();
			_discovery.Advertise(service);
		} catch (Exception e) {
			Log.Warning("HueSync: Failed to advertise emulated bridge over mDNS: " + e.Message);
		}
	}

	private Task Mode(object arg1, DynamicEventArgs arg2) {
		_devMode = (DeviceMode)arg2.Arg0;
		return Task.CompletedTask;
	}

	protected override Task ExecuteAsync(CancellationToken stoppingToken) {
		Splitter.DoSend = true;
		return Task.Run(async () => {
			try {
				while (!stoppingToken.IsCancellationRequested) {
					await Task.Delay(1000, stoppingToken);
				}
			} catch (Exception) {
				// ignored
			}

			Splitter.DoSend = false;
			Log.Information("Hue Sync stream service stopped.");
		}, stoppingToken);
	}

	public override Task StopAsync(CancellationToken cancellationToken) {
		_dtls.Stop();
		_bridge.FrameReceived -= OnFrame;
		_bridge.StreamStateChanged -= OnStreamStateChanged;
		try {
			_discovery?.Dispose();
		} catch (Exception) {
			// ignored
		}

		return Task.CompletedTask;
	}

	private void OnStreamStateChanged(bool active) {
		if (active) {
			// The TV started syncing: take over as the active source.
			if (_devMode != DeviceMode.HueSync) {
				_cs.SetMode(DeviceMode.HueSync).ConfigureAwait(false);
			}
		} else {
			_sourceActive = false;
			Splitter.DoSend = false;
		}
	}

	private void OnFrame(Color[] channelColors) {
		if (_building) {
			return;
		}

		_building = true;
		_ = ProcessFrame(channelColors);
	}

	private async Task ProcessFrame(Color[] channelColors) {
		try {
			_sourceActive = true;
			Splitter.DoSend = true;

			if (_devMode != DeviceMode.HueSync) {
				await _cs.SetMode(DeviceMode.HueSync).ConfigureAwait(false);
			}

			RestartDisableTimer();

			var builder = Builder;
			if (builder == null) {
				return;
			}

			var sectorColors = MapChannelsToSectors(channelColors);
			var frame = builder.Build(sectorColors);
			if (frame != null) {
				await Splitter.Update(frame).ConfigureAwait(false);
				frame.Dispose();
			}
		} catch (Exception e) {
			Log.Warning("HueSync: Exception processing frame: " + e.Message);
		} finally {
			_building = false;
		}
	}

	/// <summary>
	///     Maps the entertainment channel colors onto Glimmr's sector array. Channels are advertised
	///     in sector order, so channel i maps to sector i. Missing channels stay black.
	/// </summary>
	private Color[] MapChannelsToSectors(Color[] channelColors) {
		var sectors = new Color[_sectorCount];
		for (var i = 0; i < _sectorCount; i++) {
			sectors[i] = i < channelColors.Length ? channelColors[i] : Color.FromArgb(0, 0, 0);
		}

		return sectors;
	}

	private void RestartDisableTimer() {
		try {
			_disableSource.Cancel();
			_disableSource.Dispose();
		} catch (Exception) {
			// ignored
		}

		_disableSource = new CancellationTokenSource();
		_disableTask = DisableSource(_disableSource.Token);
	}

	private async Task DisableSource(CancellationToken token) {
		try {
			await Task.Delay(TimeSpan.FromSeconds(3), token);
			_sourceActive = false;
			Splitter.DoSend = false;
		} catch (OperationCanceledException) {
			// ignored - new frame arrived
		} catch (Exception e) {
			Log.Debug("HueSync: Exception disabling source: " + e.Message);
		}
	}
}
