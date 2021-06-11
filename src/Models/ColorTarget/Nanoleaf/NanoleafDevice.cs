using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Threading;
using System.Threading.Tasks;
using Glimmr.Enums;
using Glimmr.Models.Util;
using Glimmr.Services;
using Nanoleaf.Client;
using Serilog;

namespace Glimmr.Models.ColorTarget.Nanoleaf {
	public sealed class NanoleafDevice : ColorTarget, IColorTarget, IDisposable {
		public NanoleafData Data { get; set; }
		private readonly NanoleafClient _nanoleafClient;
		private readonly NanoleafStreamingClient _streamingClient;
		private bool _disposed;

		private TileLayout _layout;
		private bool _wasOn;
		private bool _logged;
		private Dictionary<int, int> _targets;
		private Stopwatch _frameWatch;
		public bool Enable { get; set; }
		
		
		public bool Testing { get; set; }
		public int Brightness { get; set; }
		public string Id { get; set; }
		public string IpAddress { get; set; }
		public string Tag { get; set; }
		public bool Streaming { get; set; }

		IColorTargetData IColorTarget.Data {
			get => Data;
			set => Data = (NanoleafData) value;
		}





		/// <summary>
		///     Use this for sending color data to the panel
		/// </summary>
		/// <param name="n"></param>
		/// <param name="colorService"></param>
		public NanoleafDevice(NanoleafData n, ColorService colorService) : base(colorService) {
			colorService.ColorSendEvent += SetColor;
			Data = n;
			SetData();
			var streamMode = n.Type == "NL29" || n.Type == "NL42" ? 2 : 1;
			Log.Debug($"Creating streaming agent with mode {streamMode}.");
			var cs = ColorService.ControlService;
			cs.RefreshSystemEvent += SetData;
			_nanoleafClient = new NanoleafClient(n.IpAddress, n.Token);
			_streamingClient = new NanoleafStreamingClient(n.IpAddress, streamMode, cs.UdpClient);
			_frameWatch = new Stopwatch();
			_disposed = false;
		}

		
		public async Task StartStream(CancellationToken ct) {
			
			if (!Enable || Streaming) {
				return;
			}
			Log.Information($"{Data.Tag}::Starting stream: {Data.Id}...");

			SetData();
			Streaming = true;
			//_wasOn = await _nanoleafClient.GetPowerStatusAsync();
			if (!_frameWatch.IsRunning) _frameWatch.Restart();			
			await _nanoleafClient.TurnOnAsync();
			_nanoleafClient.SetBrightnessAsync((int) (Brightness / 100f * 255)).ConfigureAwait(false);
			await _nanoleafClient.StartExternalAsync();
			Log.Information($"{Data.Tag}::Stream started: {Data.Id}.");
		}

		public async Task StopStream() {
			if (!Streaming) {
				return;
			}

			await FlashColor(Color.FromArgb(0, 0, 0)).ConfigureAwait(false);
			Streaming = false;
			if (_frameWatch.IsRunning) _frameWatch.Reset();
			await _nanoleafClient.TurnOffAsync().ConfigureAwait(false);
			Log.Information($"{Data.Tag}::Stream stopped: {Data.Id}.");
		}


		public void SetColor(List<Color> list, List<Color> sectors, int fadeTime, bool force = false) {
			if (!Streaming || !Enable || Testing && !force) {
				return;
			}

			if (_frameWatch.ElapsedMilliseconds < 100) return;
			_frameWatch.Restart();
			var cols = new Dictionary<int, Color>();
			foreach (var p in _targets) {
				var color = Color.FromArgb(0, 0, 0);
				if (p.Value != -1) {
					var target = p.Value;
					if (target < sectors.Count) {
						color = sectors[target];
					} else {
						//Log.Warning($"Error, trying to map {target} when count is only {sectors.Count}.");
					}
				}
				cols[p.Key] = color;
			}

			if (!_logged) Log.Debug("Definitely setting colors for nanoleaf!");
			_streamingClient.SetColorAsync(cols, 1).ConfigureAwait(false);
			if (!_logged) {
				Log.Debug("SENT");
				_logged = true;
			}
			ColorService.Counter.Tick(Id);
		}


		public Task ReloadData() {
			var newData = DataUtil.GetDevice(Id);
			Data = newData;
			SetData();
			return Task.CompletedTask;
		}

		public async Task FlashColor(Color color) {
			var cols = new Dictionary<int, Color>();
			if (_layout.PositionData != null) {
				foreach (var pd in _layout.PositionData) {
					cols[pd.PanelId] = color;
				}
			}

			await _streamingClient.SetColorAsync(cols);
		}

		public bool IsEnabled() {
			return Enable;
		}


		


		public void Dispose() {
			Dispose(true);
		}

		private void SetData() {
			var sd = DataUtil.GetSystemData();
			var oldBrightness = Data.Brightness;
			DataUtil.GetItem<int>("captureMode");
			IpAddress = Data.IpAddress;
			_layout = Data.Layout;
			_targets = new Dictionary<int, int>();
			
			Brightness = Data.Brightness;
			if (oldBrightness != Brightness) {
				_nanoleafClient.SetBrightnessAsync(Brightness);
			}

			Enable = Data.Enable;
			Id = Data.Id;
			if (!Enable) return;
			foreach (var p in _layout.PositionData) {
				if (p.ShapeType == 12) {
					continue;
				}

				if (p.TargetSector != -1) {
					var target = p.TargetSector;
					var sTarget = target;
					if ((CaptureMode) sd.CaptureMode == CaptureMode.DreamScreen) target = ColorUtil.CheckDsSectors(target);
					if (sd.UseCenter) target = ColorUtil.FindEdge(target);
					_targets[p.PanelId] = target - 1;
					Log.Debug($"Mapped {sTarget} to {target}");
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