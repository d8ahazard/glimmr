﻿using System;
using System.Collections.Generic;
using System.Drawing;
using System.Threading;
using System.Threading.Tasks;
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


		/// <summary>
		///     Use this for sending color data to the panel
		/// </summary>
		/// <param name="n"></param>
		/// <param name="colorService"></param>
		public NanoleafDevice(NanoleafData n, ColorService colorService) : base(colorService) {
			colorService.ColorSendEvent += SetColor;
			if (n != null) {
				Data = n;
				SetData(n);
				var streamMode = n.Type == "NL29" || n.Type == "NL42" ? 2 : 1;
				Log.Debug($"Creating streaming agent with mode {streamMode}.");
				var cs = ColorService.ControlService;
				_nanoleafClient = new NanoleafClient(n.IpAddress, n.Token);
				_streamingClient = new NanoleafStreamingClient(n.IpAddress, streamMode, cs.UdpClient);
			}

			_disposed = false;
		}

		public bool Enable { get; set; }

		IColorTargetData IColorTarget.Data {
			get => Data;
			set => Data = (NanoleafData) value;
		}

		public bool Testing { get; set; }
		public int Brightness { get; set; }
		public string Id { get; set; }
		public string IpAddress { get; set; }
		public string Tag { get; set; }


		public async Task StartStream(CancellationToken ct) {
			if (!Enable || Streaming) {
				return;
			}

			Log.Information($"{Data.Tag}::Starting stream: {Data.Id}...");
			Streaming = true;
			_wasOn = await _nanoleafClient.GetPowerStatusAsync();
			if (!_wasOn) {
				Log.Debug("Updating nano client power...");
				await _nanoleafClient.TurnOnAsync();
				Log.Debug("Updated...");
			}

			Log.Debug($"Setting brightness to {Brightness}.");
			await _nanoleafClient.SetBrightnessAsync((int) (Brightness / 100f * 255));
			string streamMode = "v" + Data.Type == "NL29" || Data.Type == "NL42" ? "2" : "1";
			Log.Debug($"Starting external with mode: {streamMode}");
			await _nanoleafClient.StartExternalAsync(streamMode);
			Log.Information($"{Data.Tag}::Stream started: {Data.Id}.");
		}

		public async Task StopStream() {
			if (!Enable) {
				return;
			}

			await FlashColor(Color.FromArgb(0, 0, 0));
			Streaming = false;
			if (_wasOn) {
				await _nanoleafClient.TurnOffAsync().ConfigureAwait(false);
			}

			Log.Information($"{Data.Tag}::Stream stopped: {Data.Id}.");
		}


		public void SetColor(List<Color> list, List<Color> sectors, int fadeTime, bool force = false) {
			if (!Streaming || !Enable || (Testing && !force)) {
				return;
			}
			
			var cols = new Dictionary<int, Color>();
			foreach (var p in _layout.PositionData) {
				var color = Color.FromArgb(0, 0, 0);
				if (p.TargetSector != -1) {
					var target = p.TargetSector - 1;
					target = ColorUtil.CheckDsSectors(target);
					if (target < sectors.Count) {
						color = sectors[target];
						if (!_logged) Log.Debug("Mapping sector " + target + " for " + p.PanelId);
					} else {
						Log.Warning($"Error, trying to map {target} when count is only {sectors.Count}.");
					}
				}


				cols[p.PanelId] = color;
			}

			if (!_logged) Log.Debug("Definitely setting colors for nanoleaf!");
			_streamingClient.SetColorAsync(cols, fadeTime).ConfigureAwait(false);
			if (!_logged) {
				Log.Debug("SENT");
				_logged = true;
			}
			ColorService.Counter.Tick(Id);
		}


		public Task ReloadData() {
			var newData = DataUtil.GetDevice(Id);
			SetData(newData);
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


		public bool Streaming { get; set; }


		public void Dispose() {
			Dispose(true);
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