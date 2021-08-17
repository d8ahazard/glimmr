#region

using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Glimmr.Models.Util;
using Glimmr.Services;
using LifxNetPlus;
using Serilog;

#endregion

namespace Glimmr.Models.ColorTarget.Lifx {
	public class LifxDevice : ColorTarget, IColorTarget {
		private Device B { get; }

		private readonly LifxClient? _client;
		private BeamLayout? _beamLayout;
		private int _brightness;
		private LifxData _data;
		private int[] _gammaTable;
		private bool _hasMulti;
		private int _multiplier;
		private int _multiZoneCount;

		private int _targetSector;


		public LifxDevice(LifxData d, ColorService colorService) : base(colorService) {
			DataUtil.GetItem<int>("captureMode");
			_data = d ?? throw new ArgumentException("Invalid Data");
			_brightness = _data.Brightness;
			_multiplier = _data.LedMultiplier;
			if (_multiplier == null || _multiplier == 0) {
				_multiplier = 2;
			}

			Log.Debug("Brightness is " + _brightness);
			_gammaTable = GenerateGammaTable(_data.GammaCorrection);
			_client = colorService.ControlService.GetAgent("LifxAgent");
			colorService.ColorSendEvent += SetColor;
			colorService.ControlService.RefreshSystemEvent += LoadData;
			B = new Device(d.HostName, d.MacAddress, d.Service, (uint) d.Port);
			LoadData();
		}

		public bool Streaming { get; set; }
		public bool Testing { get; set; }
		public string Id { get; private set; } = "";

		public bool Enable { get; set; }

		IColorTargetData IColorTarget.Data {
			get => _data;
			set => _data = (LifxData) value;
		}


		public async Task StartStream(CancellationToken ct) {
			if (!Enable) {
				return;
			}

			if (_client == null) {
				return;
			}

			Log.Information($"{_data.Tag}::Starting stream: {_data.Id}...");
			var col = new LifxColor(0, 0, 0);

			await _client.SetLightPowerAsync(B, true).ConfigureAwait(false);
			await _client.SetColorAsync(B, col, 2700).ConfigureAwait(false);
			Streaming = true;
			await Task.FromResult(Streaming);
			Log.Information($"{_data.Tag}::Stream started: {_data.Id}.");
		}

		public async Task FlashColor(Color color) {
			if (_client == null) {
				return;
			}

			var nC = new LifxColor(color);
			//var nC = new LifxColor {R = color.R, B = color.B, G = color.G};
			await _client.SetColorAsync(B, nC).ConfigureAwait(false);
		}


		public async Task StopStream() {
			if (!Streaming) {
				return;
			}

			Streaming = false;
			if (_client == null) {
				return;
			}

			Log.Information($"{_data.Tag}::Stopping stream.: {_data.Id}...");
			var col = new LifxColor(0, 0, 0);

			await _client.SetLightPowerAsync(B, false).ConfigureAwait(false);
			await _client.SetColorAsync(B, col, 2700).ConfigureAwait(false);
			Log.Information($"{_data.Tag}::Stream stopped: {_data.Id}.");
		}


		public Task ReloadData() {
			var newData = DataUtil.GetDevice<LifxData>(Id);
			if (newData == null) {
				return Task.CompletedTask;
			}

			_data = newData;
			LoadData();
			return Task.CompletedTask;
		}

		public void Dispose() {
		}

		public Task SetColor(object o, DynamicEventArgs args) {
			SetColor(args.Arg0, args.Arg1, args.Arg2, args.Arg3);
			return Task.CompletedTask;
		}
		
		public void SetColor(Color[] colors, Color[] list, int arg3, bool force = false) {
			if (!Streaming || !Enable || Testing && !force) {
				return;
			}

			if (_hasMulti) {
				SetColorMulti(colors);
			} else {
				SetColorSingle(list);
			}

			ColorService?.Counter.Tick(Id);
		}


		public bool IsEnabled() {
			return Enable;
		}

		private int[] GenerateGammaTable(double gamma = 2.3, int maxOut = 255) {
			const int maxIn = 255;
			var output = new int[256];
			for (var i = 0; i <= maxIn; i++) {
				output[i] = (int) (Math.Pow((float) i / maxIn, gamma) * maxOut);
			}

			return output;
		}

		private void LoadData() {
			var sd = DataUtil.GetSystemData();

			DataUtil.GetItem<int>("captureMode");

			_hasMulti = _data.HasMultiZone;
			_multiplier = _data.LedMultiplier;
			if (_multiplier == null || _multiplier == 0) {
				_multiplier = 2;
			}

			if (_hasMulti) {
				_multiZoneCount = _data.LedCount;
				_beamLayout = _data.BeamLayout;

				if (_beamLayout == null && _multiZoneCount != 0) {
					_data.GenerateBeamLayout();
					_beamLayout = _data.BeamLayout;
				}
			} else {
				var target = _data.TargetSector;
				if (sd.UseCenter) {
					target = ColorUtil.FindEdge(target + 1);
				}

				_targetSector = target;
			}

			_brightness = _data.Brightness;
			_gammaTable = GenerateGammaTable(_data.GammaCorrection);
			GenerateGammaTable(_data.GammaCorrection + .1);

			Id = _data.Id;
			Enable = _data.Enable;
		}

		private void SetColorMulti(Color[] colors) {
			if (_client == null || _beamLayout == null) {
				Log.Warning("Null client or no layout!");
				return;
			}

			var output = new List<Color>();
			foreach (var segment in _beamLayout.Segments) {
				var len = segment.LedCount;
				var segColors = ColorUtil.TruncateColors(colors, segment.Offset, len, _multiplier);
				if (segment.Repeat) {
					var col = segColors[0];
					for (var c = 0; c < len; c++) {
						segColors[c] = col;
					}
				}

				if (segment.Reverse && !segment.Repeat) {
					segColors = segColors.Reverse().ToArray();
				}

				output.AddRange(segColors);
			}

			var cols = new List<LifxColor>();
			foreach (var col in output) {
				var ar = _gammaTable[col.R];
				var ag = _gammaTable[col.G];
				var ab = _gammaTable[col.B];
				var color = Color.FromArgb(ar, ag, ab);
				var colL = new LifxColor(color, _brightness / 255f) {Kelvin = 7000};
				cols.Add(colL);
			}

			_client.SetExtendedColorZonesAsync(B, cols, 5).ConfigureAwait(false);
		}


		private void SetColorSingle(Color[] list) {
			var sectors = list;
			if (sectors == null || _client == null) {
				return;
			}

			if (_targetSector >= sectors.Length) {
				return;
			}

			var input = sectors[_targetSector];

			var nC = new LifxColor(input);
			
			_client.SetColorAsync(B, nC).ConfigureAwait(false);
			ColorService?.Counter.Tick(Id);
		}
	}
}