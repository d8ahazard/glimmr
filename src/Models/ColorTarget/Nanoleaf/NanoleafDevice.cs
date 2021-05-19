using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using Glimmr.Enums;
using Glimmr.Models.Util;
using Glimmr.Services;
using Nanoleaf.Client;
using Serilog;

namespace Glimmr.Models.ColorTarget.Nanoleaf {
	public sealed class NanoleafDevice : ColorTarget, IColorTarget, IDisposable {
		private TileLayout _layout;
		private bool _disposed;
		public bool Enable { get; set; }

		IColorTargetData IColorTarget.Data {
			get => Data;
			set => Data = (NanoleafData) value;
		}

		public NanoleafData Data { get; set; }
		public bool Testing { get; set; }
		public int Brightness { get; set; }
		public string Id { get; set; }
		public string IpAddress { get; set; }
		public string Tag { get; set; }
		private bool _wasOn;
		private readonly NanoleafClient _nanoleafClient;
		private readonly NanoleafStreamingClient _streamingClient;
		
		private List<List<Color>> _frameBuffer;
		private int _frameDelay;

		

		/// <summary>
		/// Use this for sending color data to the panel
		/// </summary>
		/// <param name="n"></param>
		/// <param name="colorService"></param>
		public NanoleafDevice(NanoleafData n, ColorService colorService) : base(colorService) {
			colorService.ColorSendEvent += SetColor;
			if (n != null) {
				Data = n;
				SetData(n);
				var streamMode = n.Type == "NL29" ? 2 : 1;
				var cs = ColorService.ControlService;
				_nanoleafClient = new NanoleafClient(n.IpAddress, n.Token);
				_streamingClient = new NanoleafStreamingClient(n.IpAddress,streamMode,cs.UdpClient);
			}

			_disposed = false;
		}
		
		
		public async Task StartStream(CancellationToken ct) {
			if (!Enable || Streaming) return;
			_frameBuffer = new List<List<Color>>();
			Streaming = true;

			Log.Debug($@"Nanoleaf: Starting stream at {IpAddress}...");
			_wasOn = await _nanoleafClient.GetPowerStatusAsync();
			if (!_wasOn) await _nanoleafClient.TurnOnAsync().ConfigureAwait(false);
			await _nanoleafClient.SetBrightnessAsync((int)(Brightness / 100f * 255)).ConfigureAwait(false);
			await _nanoleafClient.StartExternalAsync().ConfigureAwait(false);
			Log.Debug("Panel started.");
		}

		public async Task StopStream() {
			if (!Enable) return;
			Log.Debug("Nanoleaf: Stopping stream...");
			await FlashColor(Color.FromArgb(0, 0, 0));
			Streaming = false;
			if (_wasOn) await _nanoleafClient.TurnOffAsync().ConfigureAwait(false);
			Log.Debug($@"Nanoleaf: Stopped panel: {IpAddress}");
		}


		public void SetColor(List<Color> list, List<Color> sectors, int fadeTime, bool force = false) {
			if (!Streaming || !Enable || Testing && !force) {
				return;
			}
			
			if (_frameDelay > 0) {
				_frameBuffer.Add(sectors);
				if (_frameBuffer.Count < _frameDelay) return; // Just buffer till we reach our count
				sectors = _frameBuffer[0];
				_frameBuffer.RemoveAt(0);	
			}

			var cols = new Dictionary<int, Color>();
			foreach (var p in _layout.PositionData) {
				var color = Color.FromArgb(0, 0, 0);
				if (p.TargetSector != -1) {
					var target = p.TargetSector - 1;
					target = ColorUtil.CheckDsSectors(target);
					if (target < sectors.Count) {
						color = sectors[target];
					} else {
						Log.Warning($"Error, trying to map {target} when count is only {sectors.Count}.");	
					}
				}

				
				cols[p.PanelId] = color;
			}

			_streamingClient.SetColorAsync(cols, fadeTime).ConfigureAwait(false);
			ColorService.Counter.Tick(Id);
		}



		public Task ReloadData() {
			var newData = DataUtil.GetDevice(Id);
			SetData(newData);
			return Task.CompletedTask;
		}

		private void SetData(NanoleafData n) {
			var oldBrightness = Data.Brightness;
			Data = n;
			DataUtil.GetItem<int>("captureMode");
			IpAddress = n.IpAddress;
			_layout = n.Layout;
			Brightness = n.Brightness;
			if (oldBrightness != Brightness) {
				_nanoleafClient.SetBrightnessAsync(Brightness);
			}
			Enable = n.Enable;
			Id = n.Id;
			_frameDelay = Data.FrameDelay;
			_frameBuffer = new List<List<Color>>();
		}

		public async Task FlashColor(Color color) {
			var cols = new Dictionary<int, Color>();
			foreach (var pd in _layout.PositionData) {
				cols[pd.PanelId] = color;
			}
			await _streamingClient.SetColorAsync(cols);
		}

		public bool IsEnabled() {
			return Enable;
		}

        
		public bool Streaming { get; set; }

		

		
		public async Task<TileLayout> GetLayout() {
			var layout = await _nanoleafClient.GetLayoutAsync();
			return new TileLayout(layout);
		}


        public void Dispose() {
			Dispose(true);
		}

		private void Dispose(bool disposing) {
			if (_disposed) {
				return;
			}

			if (!disposing) return;
			_disposed = true;
		}
	}
}