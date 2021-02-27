using System;
using System.Collections.Generic;
using System.Drawing;
using System.Threading;
using System.Threading.Tasks;
using ColoreColor = Colore.Data.Color;
using Colore;
using Colore.Effects.Headset;
using Colore.Effects.Keyboard;
using Colore.Effects.Keypad;
using Colore.Effects.Mouse;
using Colore.Effects.Mousepad;
using Glimmr.Models.Util;
using Glimmr.Services;
using Serilog;

namespace Glimmr.Models.ColorTarget.Razer {
	public class RazerDevice : ColorTarget, IColorTarget {
		public bool Streaming { get; set; }
		public bool Testing { get; set; }
		public int Brightness { get; set; }
		public string Id { get; set; }
		public string IpAddress { get; set; }
		public string Tag { get; set; }
		public bool Enable { get; set; }
		
		private RazerData _data;

		private bool _hasChroma;
		
		IColorTargetData IColorTarget.Data {
			get => _data;
			set => _data = (RazerData) value;
		}
		
		private IChroma _chroma;

		private const int MaxKeyboardRows = KeyboardConstants.MaxRows;
		private const int MaxKeyboardCols = KeyboardConstants.MaxColumns;
		private const int MaxMousepadLeds = MousepadConstants.MaxLeds;
		private const int MaxMouseColumns = MouseConstants.MaxColumns;
		private const int MaxMouseRows = MouseConstants.MaxRows;
		private const int MaxHeadsetLeds = HeadsetConstants.MaxLeds;
		private const int MaxKeypadRows = KeypadConstants.MaxRows;
		private const int MaxKeypadColumns = KeypadConstants.MaxColumns;


		public RazerDevice(RazerData data, ColorService colorService) : base(colorService) {
			ReloadData();
			colorService.ColorSendEvent += SetColor;
			_chroma = colorService.ControlService.GetAgent<IChroma>();
			if (_chroma == null) {
				Enable = false;
				Log.Debug("No chroma agent, OS is not Windows.");
			} else {
				_hasChroma = true;
				Log.Debug("Razer device created.");	
			}
		}
		
		public Task StartStream(CancellationToken ct) {
			if (!Streaming) Streaming = true;
			return Task.CompletedTask;
		}

		public Task StopStream() {
			if (Streaming) Streaming = false;
			return Task.CompletedTask;
		}

		public void SetColor(List<Color> colors, List<Color> sectors, int fadeTime, bool force = false) {
			if (!Streaming || !Enable || !_hasChroma || Testing && !force) return;
			switch (_data.DeviceTag) {
				case "Mouse":
					var mouseMap = CreateMouseMap(colors);
					_chroma.Mouse.SetGridAsync(mouseMap).ConfigureAwait(false);
					break;
				case "Mousepad":
					var mousepadMap = CreateMousepadMap(colors);
					_chroma.Mousepad.SetCustomAsync(mousepadMap).ConfigureAwait(false);
					break;
				case "Keyboard":
					var kbMap = CreateKeyboardMap(colors);
					_chroma.Keyboard.SetCustomAsync(kbMap).ConfigureAwait(false);
					break;
				case "Keypad":
					var kpMap = CreateKeypadMap(colors);
					_chroma.Keypad.SetCustomAsync(kpMap).ConfigureAwait(false);
					break;
				case "Headset":
					var headsetMap = CreateHeadsetMap(colors);
					_chroma.Headset.SetCustomAsync(headsetMap).ConfigureAwait(false);
					break;
			}
			

		}

		public Task FlashColor(Color color) {
			_chroma?.SetAllAsync(new ColoreColor(color.R, color.G, color.B));
			return Task.CompletedTask;
		}

		public Task ReloadData() {
			Enable = true;

			_data = DataUtil.GetObject<RazerData>("RazerData");
			if (_data == null) {
				_data = new RazerData();
				DataUtil.SetObject<RazerData>("RazerData",_data);
			}
			return Task.CompletedTask;
		}

		public void Dispose() {
			_chroma?.Dispose();
		}

		private CustomKeyboardEffect CreateKeyboardMap(List<Color> colors) {
			var source = ColorUtil.TruncateColors(colors, _data.Offset, MaxKeyboardCols);
			var keyboardGrid = CustomKeyboardEffect.Create();

			// Set the Key in the second row and the fifth column to Red
			for (var y=0; y < MaxKeyboardRows; y++) {
				for (var x=0; x < MaxKeyboardCols; x++) {
					keyboardGrid[y,x] = new ColoreColor(source[x].R, source[x].G, source[x].B);
				}
			}
			
			return keyboardGrid;
		}
		
		private CustomKeypadEffect CreateKeypadMap(List<Color> colors) {
			var source = ColorUtil.TruncateColors(colors, _data.Offset, MaxKeypadColumns);
			var keypadGrid = CustomKeypadEffect.Create();
			// Set the Key in the second row and the fifth column to Red
			for (var x=0; x < MaxKeypadColumns; x++) {
				for (var y=0; y < MaxKeypadRows; y++) {
					keypadGrid[y, x] = new ColoreColor(source[x].R, source[x].G, source[x].B);
				}
			}
			return keypadGrid;
		}

		private CustomMousepadEffect CreateMousepadMap(List<Color> colors) {
			var source = ColorUtil.TruncateColors(colors, _data.Offset, MaxMousepadLeds);
			var mousepadGrid = CustomMousepadEffect.Create();
			for (var x = 0; x < MaxMousepadLeds; x++) {
				mousepadGrid[x] = new ColoreColor(source[x].R, source[x].G, source[x].B);
			}
			return mousepadGrid;
		}
		
		private CustomHeadsetEffect CreateHeadsetMap(List<Color> colors) {
			var source = ColorUtil.TruncateColors(colors, _data.Offset, MaxHeadsetLeds);
			var headsetGrid = CustomHeadsetEffect.Create();
			for (var x = 0; x < MaxHeadsetLeds; x++) {
				headsetGrid[x] = new ColoreColor(source[x].R, source[x].G, source[x].B);
			}
			return headsetGrid;
		}

		private CustomMouseEffect CreateMouseMap(List<Color> colors) {
			var source = ColorUtil.TruncateColors(colors, _data.Offset, MaxMouseColumns);
			var mouseGrid = CustomMouseEffect.Create();
			for (var x = 0; x < MaxMouseColumns; x++) {
				for (var y = 0; y < MaxMouseRows; y++) {
					mouseGrid[y,x] = new ColoreColor(source[x].R, source[x].G, source[x].B);
				}
			}
			return mouseGrid;
		}
	}
}