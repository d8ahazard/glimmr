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
using Q42.HueApi.ColorConverters.HSB;

namespace Glimmr.Models.ColorTarget.Lifx {
	public class LifxDevice : ColorTarget, IColorTarget {
		public LifxData Data { get; set; }
		private LightBulb B { get; }

		private readonly LifxClient _client;
		private bool _hasMulti;
		private int _multizoneCount;
		private int _offset;
		private bool _reverseStrip;

		private int _targetSector;
		private ColorConverter _conv;
		private BeamLayout _beamLayout;
		
		public bool Streaming { get; set; }
		public bool Testing { get; set; }
		public int Brightness { get; set; }
		private double _scaledBrightness;
		public string Id { get; set; }
		public string IpAddress { get; set; }
		public string Tag { get; set; }


		public LifxDevice(LifxData d, ColorService colorService) : base(colorService) {
			DataUtil.GetItem<int>("captureMode");
			Data = d ?? throw new ArgumentException("Invalid Data");
			_hasMulti = d.HasMultiZone;
			_offset = d.Offset;
			_reverseStrip = d.ReverseStrip;
			if (_hasMulti) {
				_multizoneCount = d.MultiZoneCount;
				_beamLayout = d.BeamLayout;
				if (_beamLayout == null && _multizoneCount != 0) {
					d.GenerateBeamLayout();
					_beamLayout = d.BeamLayout;
				}
			}

			_client = colorService.ControlService.GetAgent("LifxAgent");
			colorService.ColorSendEvent += SetColor;
			B = new LightBulb(d.HostName, d.MacAddress, d.Service, (uint) d.Port);
			_targetSector = Data.TargetSector - 1;
			_targetSector = ColorUtil.CheckDsSectors(_targetSector);
			Brightness = d.Brightness;
			_scaledBrightness = Brightness / 100d;
			Log.Debug("Scaled bright is " + _scaledBrightness);
			Id = d.Id;
			IpAddress = d.IpAddress;
			Enable = Data.Enable;
		}

		public bool Enable { get; set; }

		IColorTargetData IColorTarget.Data {
			get => Data;
			set => Data = (LifxData) value;
		}

		
		public async Task StartStream(CancellationToken ct) {
			if (!Enable) {
				return;
			}

			Log.Information($"{Data.Tag}::Starting stream: {Data.Id}...");
			// Recalculate target sector before starting stream, just in case.
			_targetSector = Data.TargetSector - 1;
			_targetSector = ColorUtil.CheckDsSectors(_targetSector);
			var col = new LifxColor(0, 0, 0);
			//var col = new LifxColor {R = 0, B = 0, G = 0};
			_client.SetLightPowerAsync(B, true);
			_client.SetColorAsync(B, col, 2700);
			Streaming = true;
			await Task.FromResult(Streaming);
			Log.Information($"{Data.Tag}::Stream started: {Data.Id}.");
		}

		public async Task FlashColor(Color color) {
			var nC = new LifxColor(color);
			//var nC = new LifxColor {R = color.R, B = color.B, G = color.G};
			await _client.SetColorAsync(B, nC).ConfigureAwait(false);
		}


		public bool IsEnabled() {
			return Enable;
		}


		public async Task StopStream() {
			if (!Streaming) {
				return;
			}

			Streaming = false;
			if (_client == null) {
				return;
			}

			Log.Information($"{Data.Tag}::Stopping stream.: {Data.Id}...");
			_client.SetColorAsync(B, new LifxColor(Color.FromArgb(0,0,0))).ConfigureAwait(false);
			_client.SetLightPowerAsync(B, false).ConfigureAwait(false);
			Log.Information($"{Data.Tag}::Stream stopped: {Data.Id}.");
		}

		public Task ReloadData() {
			var newData = DataUtil.GetDevice<LifxData>(Id);
			DataUtil.GetItem<int>("captureMode");
			Data = newData;
			_hasMulti = Data.HasMultiZone;
			_offset = Data.Offset;
			_reverseStrip = Data.ReverseStrip;
			if (_hasMulti) {
				_multizoneCount = Data.LedCount;
				if (_beamLayout == null && _multizoneCount != 0) {
					Data.GenerateBeamLayout();
					_beamLayout = Data.BeamLayout;
				}
			}

			IpAddress = Data.IpAddress;
			var targetSector = newData.TargetSector;
			_targetSector = targetSector - 1;
			var oldBrightness = Brightness;
			Brightness = newData.Brightness;
			_scaledBrightness = Brightness / 100d;
			Log.Debug("Scaled is " + _scaledBrightness);
			if (oldBrightness != Brightness) {
				var bri = Brightness / 100 * 255;
				_client.SetBrightnessAsync(B, (ushort) bri).ConfigureAwait(false);
			}

			Id = newData.Id;
			Enable = Data.Enable;
			return Task.CompletedTask;
		}

		public void Dispose() {
		}

		public void SetColor(List<Color> colors, List<Color> list, int arg3, bool force = false) {
			if (!Streaming || !Enable || Testing && !force) {
				return;
			}

			if (_hasMulti) {
				SetColorMulti(colors);
			} else {
				SetColorSingle(list);
			}

			ColorService.Counter.Tick(Id);
		}

		private void SetColorMulti(List<Color> colors) {
			if (_client == null || _beamLayout == null) {
				Log.Warning("Null client or no layout!");
				return;
			}

			var output = new List<Color>();
			foreach (var segment in _beamLayout.Segments) {
				var len = segment.LedCount;
				var segColors = ColorUtil.TruncateColors(colors, segment.Offset, len * 2);
				if (segment.Repeat) {
					var col = segColors[0];
					for (var c = 0; c < len * 2; c++) {
						segColors[c] = col;
					}
				}
				
				if (segment.Reverse && !segment.Repeat) segColors = segColors.Reverse().ToArray();
				output.AddRange(segColors);
			}

			var i = 0;
			
			var cols = new List<LifxColor>();

			foreach (var col in output) {
				if (i == 0) {
					cols.Add(new LifxColor(col, _scaledBrightness));
					i = 1;
				} else {
					i = 0;
				}
			}

			//if (true) cols.Reverse();
			//Log.Debug("Sending...");
			_client.SetExtendedColorZonesAsync(B, cols,5).ConfigureAwait(false);
			//Log.Debug("Scent");
		}

		

		private void SetColorSingle(List<Color> list) {
			var sectors = list;
			if (sectors == null || _client == null) {
				return;
			}

			if (_targetSector >= sectors.Count) {
				return;
			}

			var input = sectors[_targetSector];

			var nC = new LifxColor(input);
			//var nC = new LifxColor {R = input.R, B = input.B, G = input.G};

			_client.SetColorAsync(B, nC).ConfigureAwait(false);
			ColorService.Counter.Tick(Id);
		}
	}
}