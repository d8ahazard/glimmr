#region

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Glimmr.Enums;
using Glimmr.Models;
using Glimmr.Models.ColorSource;
using Glimmr.Models.ColorTarget;
using Glimmr.Models.Util;
using Microsoft.Extensions.Hosting;
using Newtonsoft.Json;
using Serilog;

// ReSharper disable All

#endregion

namespace Glimmr.Services {
	// Handles capturing and sending color data
	public class ColorService : BackgroundService {
		public ControlService ControlService { get; }
		private const int DeviceGroup = 20;

		public FrameCounter Counter;
		public DeviceMode DeviceMode;
		private bool _autoDisabled;
		private int _autoDisableDelay;
		private CaptureMode _captureMode;
		private TimeSpan _frameSpan;
		private Stopwatch _frameWatch;

		// Figure out how to make these generic, non-callable

		private IColorTarget[] _sDevices;

		// Token for the color target
		private CancellationTokenSource _sendTokenSource;

		private bool _setAutoDisable;

		// Generates a token every time we send colors, and expires super-fast
		private CancellationToken _stopToken;
		private Dictionary<DeviceMode, IColorSource> _streams;
		private bool _streamStarted;

		// Token for the color source
		private CancellationTokenSource _streamTokenSource;

		//private float _fps;
		private SystemData _systemData;
		private Stopwatch _watch;

		public ColorService(ControlService controlService) {
			_frameSpan = TimeSpan.FromMilliseconds(1000f / 60);
			_systemData = DataUtil.GetSystemData();
			_streams = new Dictionary<DeviceMode, IColorSource>();
			ControlService = controlService;
			Counter = new FrameCounter(this);
			ControlService.ColorService = this;
			ControlService.TriggerSendColorEvent += SendColors;
			ControlService.SetModeEvent += Mode;
			ControlService.DeviceReloadEvent += RefreshDeviceData;
			ControlService.RefreshLedEvent += ReloadLedData;
			ControlService.RefreshSystemEvent += ReloadSystemData;
			ControlService.TestLedEvent += LedTest;
			ControlService.FlashDeviceEvent += FlashDevice;
			ControlService.FlashSectorEvent += FlashSector;
			ControlService.DemoLedEvent += Demo;
		}

		public event Action<List<Color>, List<Color>, int, bool> ColorSendEvent = delegate { };

		protected override Task ExecuteAsync(CancellationToken stoppingToken) {
			Log.Information("Starting color service...");
			_stopToken = stoppingToken;
			_watch = new Stopwatch();
			_frameWatch = new Stopwatch();
			_streamTokenSource = new CancellationTokenSource();
			LoadData();
			return Task.Run(async () => {
				await Demo(this, new DynamicEventArgs()).ConfigureAwait(false);
				Log.Information($"All color sources initialized, setting mode to {DeviceMode}.");
				await Mode(this, new DynamicEventArgs(DeviceMode)).ConfigureAwait(true);
				while (!stoppingToken.IsCancellationRequested) {
					await CheckAutoDisable();
					if (_setAutoDisable) {
						if (!_watch.IsRunning) _watch.Restart();
						if (_watch.ElapsedMilliseconds >= _autoDisableDelay * 1000f) {
							_autoDisabled = true;
							_setAutoDisable = false;
							DataUtil.SetItem("AutoDisabled", _autoDisabled);
							ControlService.SetModeEvent -= Mode;
							await ControlService.SetMode(0);
							ControlService.SetModeEvent += Mode;
							Log.Information(
								$"Auto-disabling stream {_watch.ElapsedMilliseconds} vs {_autoDisableDelay * 1000}.");
							_watch.Reset();
						}
					} else {
						_watch.Reset();
					}

					await Task.Delay(500, stoppingToken);
				}
			}, CancellationToken.None);
		}

		public override async Task StopAsync(CancellationToken cancellationToken) {
			Log.Information("Stopping color service...");
			await StopServices();
			DataUtil.Dispose();
			Log.Information("Color service stopped.");
			await base.StopAsync(cancellationToken);
		}

		public void AddStream(DeviceMode mode, BackgroundService stream) {
			_streams[mode] = (IColorSource) stream;

			_streams[mode].Refresh(_systemData);
		}

		public BackgroundService GetStream(DeviceMode name) {
			if (_streams.ContainsKey(name)) return (BackgroundService) _streams[name];
			return null;
		}

		private async Task FlashDevice(object o, DynamicEventArgs dynamicEventArgs) {
			var devId = dynamicEventArgs.P1;
			var disable = false;
			var ts = new CancellationTokenSource();
			var lc = 300;
			try {
				lc = _systemData.LedCount;
			} catch (Exception) {
			}

			var bColor = Color.FromArgb(0, 0, 0, 0);
			var rColor = Color.FromArgb(255, 255, 0, 0);
			for (var i = 0; i < _sDevices.Length; i++) {
				if (_sDevices[i].Id == devId) {
					var sd = _sDevices[i];
					sd.Testing = true;
					Log.Information("Flashing device: " + devId);
					if (!sd.Streaming) {
						disable = true;
						await sd.StartStream(ts.Token);
					}

					await sd.FlashColor(rColor);
					Thread.Sleep(500);
					await sd.FlashColor(bColor);
					Thread.Sleep(500);
					await sd.FlashColor(rColor);
					Thread.Sleep(500);
					await sd.FlashColor(bColor);
					sd.Testing = false;
					if (!disable) {
						continue;
					}

					await sd.StopStream();
					ts.Cancel();
					ts.Dispose();
				}
			}
		}

		private async Task FlashSector(object o, DynamicEventArgs dynamicEventArgs) {
			var sector = dynamicEventArgs.P1;
			var col = Color.FromArgb(255, 255, 0, 0);
			Color[] colors = ColorUtil.AddLedColor(new Color[_systemData.LedCount], sector, col, _systemData);
			var sectorCount = ((_systemData.HSectors + _systemData.VSectors) * 2) - 4;
			var sectors = ColorUtil.EmptyColors(new Color[sectorCount]);
			if (sector < sectorCount) sectors[sector] = col;
			var black = ColorUtil.EmptyList(_systemData.LedCount);
			var blackSectors = ColorUtil.EmptyList(sectorCount);
			var devices = new List<IColorTarget>();
			for (var i = 0; i < _sDevices.Length; i++) {
				if (_sDevices[i].Enable) {
					devices.Add((IColorTarget) _sDevices[i]);
				}
			}

			foreach (var _strip in devices) {
				if (_strip != null) {
					_strip.Testing = true;
				}
			}

			foreach (var _strip in devices) {
				if (_strip != null) {
					_strip.SetColor(colors.ToList(), sectors.ToList(), 0);
				}
			}

			Thread.Sleep(500);

			foreach (var _strip in devices) {
				if (_strip != null) {
					_strip.SetColor(black, blackSectors, 0);
				}
			}

			foreach (var _strip in devices) {
				if (_strip != null) {
					_strip.SetColor(colors.ToList(), sectors.ToList(), 0);
				}
			}

			Thread.Sleep(1000);

			foreach (var _strip in devices) {
				if (_strip != null) {
					_strip.SetColor(black, blackSectors, 0);
					_strip.Testing = false;
				}
			}
		}

		private async Task CheckAutoDisable() {
			var sourceActive = false;
			// If we're in video or audio mode, check the source is active...
			if (DeviceMode == DeviceMode.Video && _streams[DeviceMode.Video] != null &&
			    _captureMode != CaptureMode.DreamScreen) {
				sourceActive = _streams[DeviceMode.Video].SourceActive;
			} else {
				//todo: Add proper source checks for other media. 
				sourceActive = true;
			}

			if (sourceActive) {
				if (!_autoDisabled) return;
				Log.Information("Auto-enabling stream.");
				_autoDisabled = false;
				DataUtil.SetItem("AutoDisabled", _autoDisabled);
				ControlService.SetModeEvent -= Mode;
				await ControlService.SetMode((int) DeviceMode);
				ControlService.SetModeEvent += Mode;
				_watch.Reset();
				_setAutoDisable = false;
			} else {
				if (_autoDisabled || DeviceMode != DeviceMode.Video) return;
				_setAutoDisable = true;
			}
		}


		private async Task LedTest(object o, DynamicEventArgs dynamicEventArgs) {
			int led = dynamicEventArgs.P1;
			var strips = new List<IColorTarget>();
			for (var i = 0; i < _sDevices.Length; i++) {
				if (_sDevices[i].Enable == true) {
					strips.Add((IColorTarget) _sDevices[i]);
				}
			}

			var colors = ColorUtil.EmptyList(_systemData.LedCount);
			var blackColors = colors;
			colors[led] = Color.FromArgb(255, 0, 0);
			var sectors = ColorUtil.LedsToSectors(colors, _systemData);
			var blackSectors = ColorUtil.EmptyList(_systemData.SectorCount);
			foreach (var dev in _sDevices) {
				dev.Testing = true;
				dev.SetColor(colors, sectors, 0, true);
			}

			await Task.Delay(500);
			foreach (var dev in _sDevices) {
				dev.SetColor(blackColors, blackSectors, 0, true);
			}

			await Task.Delay(500);
			foreach (var dev in _sDevices) {
				dev.SetColor(colors, sectors, 0, true);
			}

			await Task.Delay(500);
			foreach (var dev in _sDevices) {
				dev.SetColor(blackColors, blackSectors, 0, true);
				dev.Testing = false;
			}
		}

		private void LoadData() {
			// Reload main vars
			DeviceMode = (DeviceMode) DataUtil.GetItem<int>("DeviceMode");
			_captureMode = (CaptureMode) (DataUtil.GetItem<int>("CaptureMode") ?? 2);
			_sendTokenSource = new CancellationTokenSource();
			_systemData = DataUtil.GetSystemData();
			_autoDisableDelay = _systemData.AutoDisableDelay;
			// Create new lists
			var sDevs = new List<IColorTarget>();
			var classes = SystemUtil.GetClasses<IColorTarget>();
			var deviceData = DataUtil.GetDevices();
			var enabled = 0;
			foreach (var c in classes) {
				try {
					var tag = c.Replace("Device", "");
					var dataName = c.Replace("Device", "Data");
					tag = c.Replace("Glimmr.Models.ColorTarget.", "");
					tag = tag.Split(".")[0];
					foreach (var device in deviceData) {
						if (device.Tag == tag) {
							if (device.Enable) enabled++;
							Log.Debug($"Creating {device.Tag}: {device.Id}");
							var args = new object[] {device, this};
							var obj = (IColorTarget) Activator.CreateInstance(Type.GetType(c)!, args);
							sDevs.Add(obj);
						}
					}
				} catch (InvalidCastException e) {
					Log.Warning("Exception: " + e.Message + " at " + e.StackTrace);
				}
			}

			_sDevices = sDevs.ToArray();
		}

		private async Task Demo(object o, DynamicEventArgs dynamicEventArgs) {
			await StartStream();
			var ledCount = 300;
			var sectorCount = _systemData.SectorCount;
			try {
				ledCount = _systemData.LedCount;
			} catch (Exception) {
			}

			var i = 0;
			var cols = new List<Color>();
			var secs = new List<Color>();
			cols = ColorUtil.EmptyList(ledCount);
			secs = ColorUtil.EmptyList(sectorCount);
			var degs = ledCount / sectorCount;
			try {
				while (i < ledCount) {
					var pi = i * 1.0f;
					var progress = pi / ledCount;
					var sector = (int) Math.Round(progress * (float)sectorCount);
					var rCol = ColorUtil.Rainbow(progress);
					cols[i] = rCol;
					if (sector < secs.Count) secs[sector] = rCol;
					try {
						SendColors(cols, secs, 0, true);
					} catch (Exception e) {
						Log.Warning("SEND EXCEPTION: " + JsonConvert.SerializeObject(e));
					}

					i++;
				}
			} catch (Exception f) {
				Log.Warning("Outer demo exception: " + f.Message);
			}

			await Task.Delay(500);
		}


		private async Task RefreshDeviceData(object o, DynamicEventArgs dynamicEventArgs) {
			var id = dynamicEventArgs.P1;
			if (string.IsNullOrEmpty(id)) {
				Log.Warning("Can't refresh null device: " + id);
			} else {
				Log.Debug("Refreshing device: " + id);
			}

			foreach (var dev in _sDevices) {
				if (dev.Data.Id == id) {
					Log.Debug("Reloading device: " + id);
					var wasStreaming = dev.Streaming;
					await dev.ReloadData().ConfigureAwait(false);
					if (DeviceMode != DeviceMode.Off && dev.Data.Enable && !dev.Streaming) {
						dev.StartStream(_sendTokenSource.Token).ConfigureAwait(false);
					}
					
					if (DeviceMode != DeviceMode.Off && !dev.Data.Enable && dev.Streaming) {
						Log.Debug("Stopping disabled device: " + dev.Id);
						dev.StopStream().ConfigureAwait(false);
					}

					await Task.FromResult(true);
					return;
				}
			}

			var sda = DataUtil.GetDevice(id);

			// If our device is a real boy, start it and add it
			if (sda == null) {
				return;
			}

			var newDev = CreateDevice(sda);
			await newDev.StartStream(_sendTokenSource.Token).ConfigureAwait(false);

			var sDevs = _sDevices.ToList();
			sDevs.Add(newDev);
			_sDevices = sDevs.ToArray();
		}

		private dynamic CreateDevice(dynamic devData) {
			var classes = SystemUtil.GetClasses<IColorTarget>();
			var deviceData = DataUtil.GetDevices();
			foreach (var c in classes) {
				try {
					var tag = c.Replace("Device", "");
					var dataName = c.Replace("Device", "Data");
					tag = c.Replace("Glimmr.Models.ColorTarget.", "");
					tag = tag.Split(".")[0];
					if (devData.Tag == tag) {
						Log.Debug($"Creating {devData.Tag}: {devData.Id}");
						var args = new object[] {devData, this};
						var obj = (IColorTarget) Activator.CreateInstance(Type.GetType(c)!, args);
						return obj;
					}
				} catch (Exception e) {
					Log.Warning("Exception: " + e.Message);
				}
			}

			return null;
		}

		private Task ReloadLedData(object o, DynamicEventArgs dynamicEventArgs) {
			string ledId = dynamicEventArgs.P1;
			foreach (var dev in _sDevices) {
				if (dev.Id == ledId) {
					dev.ReloadData();
				}
			}

			return Task.CompletedTask;
		}


		private void ReloadSystemData() {
			var sd = _systemData;
			_systemData = DataUtil.GetSystemData();
			foreach (var stream in _streams.Values) {
				stream.Refresh(_systemData);
			}

			_autoDisableDelay = sd.AutoDisableDelay;
			if (_autoDisableDelay < 1) {
				_autoDisableDelay = 10;
			}

			var prevCapMode = _captureMode;
			_captureMode = (CaptureMode) sd.CaptureMode;
		}

		private async Task Mode(object o, DynamicEventArgs dynamicEventArgs) {
			var newMode = (DeviceMode) dynamicEventArgs.P1;
			DeviceMode = newMode;
			if (newMode != 0 && _autoDisabled) {
				_autoDisabled = false;
				DataUtil.SetItem("AutoDisabled", _autoDisabled);
			}

			if (_streamStarted && newMode == 0) await StopStream();
			
			foreach (var stream in _streams) {
				if (newMode == DeviceMode.Video &&
				    stream.Key == DeviceMode.DreamScreen &&
				    (CaptureMode) _systemData.CaptureMode == CaptureMode.DreamScreen) {
					stream.Value.ToggleStream(true);
				} else {
					stream.Value.ToggleStream(stream.Key == newMode);
				}
			}

			if (newMode != 0 && !_streamStarted) await StartStream();
			DeviceMode = newMode;
			Log.Information($"Device mode updated to {newMode}.");
		}


		private Task StartStream() {
			if (!_streamStarted) {
				_streamStarted = true;
				Log.Information("Starting streaming devices...");
				foreach (var sdev in _sDevices) {
					if (sdev.Data == null) {
						Log.Debug("SET DEV DATA: " + sdev.Id);
					}
					if (sdev.Data.Tag != "Led" && sdev.Data.Enable) {
						if (!SystemUtil.IsOnline(sdev.Data.IpAddress)) {
							Log.Debug($"Device {sdev.Data.Tag} at {sdev.Data.Id} is offline.");
							continue;
						}
					}

					sdev.StartStream(_sendTokenSource.Token);
				}
			}

			if (_streamStarted) {
				Log.Information("Streaming on all devices should now be started...");
			}

			return Task.CompletedTask;
		}

		private async Task StopStream() {
			if (!_streamStarted) {
				return;
			}

			_streamStarted = false;
			var streamers = new List<IColorTarget>();
			foreach (var s in _sDevices.Where(s => s.Streaming)) streamers.Add(s);
			await Task.WhenAll(streamers.Select(i => {
				try {
					return i.StopStream();
				} catch (Exception e) {
					Log.Warning("Well, this is exceptional: " + e.Message);
					return Task.CompletedTask;
				}
			})).ConfigureAwait(false);
			Log.Information("Stream stopped.");
		}


		public void SendColors(List<Color> colors, List<Color> sectors, int fadeTime, bool force = false) {
			_sendTokenSource ??= new CancellationTokenSource();
			if (_sendTokenSource.IsCancellationRequested) {
				return;
			}

			if (!_streamStarted) {
				return;
			}

			if (!_frameWatch.IsRunning) {
				_frameWatch.Start();
			}

			if (_frameWatch.Elapsed >= _frameSpan || force) {
				_frameWatch.Restart();
				Counter.Tick("source");
				ColorSendEvent(colors, sectors, fadeTime, force);
			}
		}


		private static void CancelSource(CancellationTokenSource target, bool dispose = false) {
			if (target == null) return;

			if (!target.IsCancellationRequested) target.CancelAfter(0);

			if (dispose) target.Dispose();
		}

		private async Task StopServices() {
			Log.Information("Stopping services...");
			await StopStream().ConfigureAwait(false);
			_watch.Stop();
			_frameWatch.Stop();
			_streamTokenSource?.Cancel();
			foreach (var stream in _streams) {
				stream.Value.ToggleStream(false);
			}

			CancelSource(_sendTokenSource, true);
			foreach (var s in _sDevices) {
				try {
					s?.Dispose();
				} catch (Exception e) {
					Log.Warning("Caught exception: " + e.Message);
				}
			}

			Log.Information("All services have been stopped.");
		}
	}
}