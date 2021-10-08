#region

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Threading;
using System.Threading.Tasks;
using Glimmr.Models.Util;
using Glimmr.Services;
using Nanoleaf.Client;
using Serilog;

#endregion

namespace Glimmr.Models.ColorTarget.Nanoleaf {
	public sealed class NanoleafDevice : ColorTarget, IColorTarget, IDisposable {
		private readonly Stopwatch _frameWatch;
		private readonly NanoleafClient? _nanoleafClient;
		private readonly NanoleafStreamingClient? _streamingClient;
		private int _brightness;
		private NanoleafData _data;
		private bool _disposed;
		private TileLayout? _layout;
		private Dictionary<int, int> _targets;
		private int _frameTime;

		/// <summary>
		///     Use this for sending color data to the panel
		/// </summary>
		/// <param name="n"></param>
		/// <param name="cs"></param>
		public NanoleafDevice(NanoleafData n, ColorService cs) : base(cs) {
			_brightness = -1;
			_targets = new Dictionary<int, int>();
			_data = n;
			Id = _data.Id;
			var streamMode = n.Type is "NL29" or "NL42" ? 2 : 1;
			var controlService = cs.ControlService;
			var host = n.IpAddress;
			try {
				var ip = IpUtil.GetIpFromHost(n.IpAddress);
				if (ip != null) {
					host = ip.ToString();
				} else {
					if (host.Contains(".local")) {
						host = host.Replace(".local", "");
						ip = IpUtil.GetIpFromHost(host);
					}
				}

				if (ip != null) host = ip.ToString();
			} catch (Exception) {
				//ignored
			}
			try {
				
				Log.Debug("Creating nano client: " + host);
				_nanoleafClient = new NanoleafClient(host, n.Token);
				Log.Debug("Nano client created.");
				_streamingClient = new NanoleafStreamingClient(host, streamMode, controlService.UdpClient);
			} catch (Exception e) {
				Log.Debug("Exception creating client..." + e.Message);
			}

			_frameWatch = new Stopwatch();
			SetData();
			controlService.RefreshSystemEvent += SetData;
			_disposed = false;
			cs.ColorSendEventAsync += SetColors;
		}

		public bool Enable { get; set; }
		public bool Testing { get; set; }
		public string Id { get; private set; }
		public bool Streaming { get; set; }

		IColorTargetData IColorTarget.Data {
			get => _data;
			set => _data = (NanoleafData) value;
		}


		public async Task StartStream(CancellationToken ct) {
			if (!Enable || Streaming) {
				return;
			}

			if (_nanoleafClient == null || _streamingClient == null) {
				Log.Warning("Client is null...");
				return;
			}

			Log.Debug($"{_data.Tag}::Starting stream: {_data.Id}...");
			SetData();
			Streaming = true;
			if (!_frameWatch.IsRunning) {
				_frameWatch.Restart();
			}

			await _nanoleafClient.StartExternalAsync();
			await _nanoleafClient.SetBrightnessAsync(_brightness);
			Log.Debug($"{_data.Tag}::Stream started: {_data.Id}.");
		}

		public async Task StopStream() {
			if (!Streaming) {
				return;
			}

			await FlashColor(Color.FromArgb(0, 0, 0)).ConfigureAwait(false);
			Streaming = false;
			_frameWatch.Restart();
			if (_nanoleafClient == null || _streamingClient == null) {
				Log.Warning("Client is null...");
				return;
			}

			
			await _nanoleafClient.TurnOffAsync().ConfigureAwait(false);
			Log.Debug($"{_data.Tag}::Stream stopped: {_data.Id}.");
		}

		public Task ReloadData() {
			var newData = DataUtil.GetDevice(Id);
			if (newData == null) {
				return Task.CompletedTask;
			}

			_data = newData;
			SetData();
			return Task.CompletedTask;
		}

		public async Task FlashColor(Color color) {
			var cols = new Dictionary<int, Color>();
			if (_layout == null) {
				return;
			}

			if (_layout.PositionData != null) {
				foreach (var pd in _layout.PositionData) {
					cols[pd.PanelId] = color;
				}
			}
			if (_nanoleafClient == null || _streamingClient == null) {
				Log.Warning("Client is null...");
				return;
			}

			await _streamingClient.SetColorAsync(cols);
		}


		public void Dispose() {
			Dispose(true);
		}

		private Task SetColors(object sender, ColorSendEventArgs args) {
			return SetColor(args.SectorColors, args.Force);
		}


		private async Task SetColor(IReadOnlyList<Color> sectors, bool force = false) {
			if (!Streaming || !Enable || Testing && !force) {
				return;
			}

			if (_frameWatch.ElapsedMilliseconds < _frameTime) {
				return;
			}

			_frameWatch.Restart();
			
			var cols = new Dictionary<int, Color>();
			foreach (var (key, target) in _targets) {
				var color = Color.FromArgb(0, 0, 0);
				if (target != -1) {
					if (target < sectors.Count) {
						color = sectors[target];
					}
				}

				cols[key] = color;
			}

			if (_nanoleafClient == null || _streamingClient == null) {
				Log.Warning("Client is null...");
				return;
			}

			await _streamingClient.SetColorAsync(cols, 1).ConfigureAwait(false);
			ColorService?.Counter.Tick(Id);
		}

		private void SetData() {
			var sd = DataUtil.GetSystemData();
			DataUtil.GetItem<int>("captureMode");
			_layout = _data.Layout;
			_frameTime = _data.Type == "NL42" ? 100 : 40;
			_targets = new Dictionary<int, int>();
			if (_data.Brightness != _brightness) {
				_brightness = _data.Brightness;
				if (_nanoleafClient == null || _streamingClient == null) {
					Log.Warning("Client is null...");
					return;
				}

				_nanoleafClient.SetBrightnessAsync(_brightness).ConfigureAwait(false);
			}

			Enable = _data.Enable;
			Id = _data.Id;
			if (!Enable) {
				return;
			}

			if (_layout?.PositionData == null) {
				return;
			}

			foreach (var p in _layout.PositionData) {
				if (p.ShapeType == 12) {
					continue;
				}

				if (p.TargetSector != -1) {
					var target = p.TargetSector;

					if (sd.UseCenter) {
						target = ColorUtil.FindEdge(target);
					}

					_targets[p.PanelId] = target - 1;
				} else {
					_targets[p.PanelId] = -1;
				}
			}
		}


		public async Task<TileLayout?> GetLayout() {
			if (_nanoleafClient == null || _streamingClient == null) {
				Log.Warning("Client is null...");
				return null;
			}

			var layout = await _nanoleafClient.GetLayoutAsync();
			return new TileLayout(layout);
		}

		private void Dispose(bool disposing) {
			if (_disposed) {
				return;
			}

			if (!disposing) {
				return;
			}

			_disposed = true;
		}
	}
}