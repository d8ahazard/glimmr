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
		private readonly NanoleafClient _nanoleafClient;
		private readonly NanoleafStreamingClient _streamingClient;
		private int _brightness = 255;
		private NanoleafData _data;
		private bool _disposed;

		private TileLayout? _layout;
		private Dictionary<int, int> _targets;


		/// <summary>
		///     Use this for sending color data to the panel
		/// </summary>
		/// <param name="n"></param>
		/// <param name="colorService"></param>
		public NanoleafDevice(NanoleafData n, ColorService colorService) : base(colorService) {
			colorService.ColorSendEvent1 += SetColor;
			ColorService = colorService;
			_targets = new Dictionary<int, int>();
			_data = n;
			SetData();
			var streamMode = n.Type == "NL29" || n.Type == "NL42" ? 2 : 1;
			var cs = ColorService.ControlService;
			cs.RefreshSystemEvent += SetData;
			_nanoleafClient = new NanoleafClient(n.IpAddress, n.Token);
			_streamingClient = new NanoleafStreamingClient(n.IpAddress, streamMode, cs.UdpClient);
			_frameWatch = new Stopwatch();
			_disposed = false;
		}

		public bool Enable { get; set; }
		public bool Testing { get; set; }
		public string Id { get; private set; } = "";
		public bool Streaming { get; set; }

		IColorTargetData IColorTarget.Data {
			get => _data;
			set => _data = (NanoleafData) value;
		}


		public async Task StartStream(CancellationToken ct) {
			if (!Enable || Streaming) {
				return;
			}

			Log.Information($"{_data.Tag}::Starting stream: {_data.Id}...");

			SetData();
			Streaming = true;
			//_wasOn = await _nanoleafClient.GetPowerStatusAsync();
			if (!_frameWatch.IsRunning && _data.Type == "NL42") {
				_frameWatch.Restart();
			}

			//await _nanoleafClient.TurnOnAsync();
			//await _nanoleafClient.SetBrightnessAsync((int) (Brightness / 100f * 255));
			await _nanoleafClient.StartExternalAsync();
			Log.Information($"{_data.Tag}::Stream started: {_data.Id}.");
		}

		public async Task StopStream() {
			if (!Streaming) {
				return;
			}

			await FlashColor(Color.FromArgb(0, 0, 0)).ConfigureAwait(false);
			Streaming = false;
			if (_frameWatch.IsRunning && _data.Type == "NL42") {
				_frameWatch.Reset();
			}

			await _nanoleafClient.TurnOffAsync().ConfigureAwait(false);
			Log.Information($"{_data.Tag}::Stream stopped: {_data.Id}.");
		}


		public Task SetColor(object o, DynamicEventArgs args) {
						SetColor(args.Arg0, args.Arg1, args.Arg2, args.Arg3);
			return Task.CompletedTask;
		}

	public void SetColor(Color[] list, Color[] sectors, int fadeTime, bool force = false) {
			if (!Streaming || !Enable || Testing && !force) {
				return;
			}

			if (_frameWatch.ElapsedMilliseconds < 100 && _data.Type == "NL42") {
				return;
			}

			if (_data.Type == "NL42") {
				_frameWatch.Restart();
			}

			var cols = new Dictionary<int, Color>();
			foreach (var p in _targets) {
				var color = Color.FromArgb(0, 0, 0);
				if (p.Value != -1) {
					var target = p.Value;
					if (target < sectors.Length) {
						color = sectors[target];
					}
				}

				cols[p.Key] = color;
			}


			_streamingClient.SetColorAsync(cols, 1).ConfigureAwait(false);

			ColorService?.Counter.Tick(Id);
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

			await _streamingClient.SetColorAsync(cols);
		}


		public void Dispose() {
			Dispose(true);
		}

		public bool IsEnabled() {
			return Enable;
		}

		private void SetData() {
			var sd = DataUtil.GetSystemData();
			var oldBrightness = _data.Brightness;
			DataUtil.GetItem<int>("captureMode");
			_layout = _data.Layout;
			_targets = new Dictionary<int, int>();

			_brightness = _data.Brightness;
			if (oldBrightness != _brightness) {
				_nanoleafClient.SetBrightnessAsync(_brightness);
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


		public async Task<TileLayout> GetLayout() {
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