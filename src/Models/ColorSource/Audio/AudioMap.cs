using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using Emgu.CV.Structure;
using Glimmr.Models.Util;
using Newtonsoft.Json;
using Serilog;

namespace Glimmr.Models.ColorSource.Audio {
	public class AudioMap {
		private int _vSectors;
		private int _hSectors;
		private float _rotationSpeed;
		private float _rotation;
		private float _rotationThreshold;
		private float _rotationUpper = 1;
		private float _rotationLower;
		private RangeF _highRange;
		private RangeF _lowRange;
		private RangeF _midRange;
		private List<int> _rightSectors;
		private List<int> _leftSectors;
		private bool _triggered;
		private readonly JsonLoader _loader;
		
		public AudioMap() {
			_loader = new JsonLoader("audioScenes");
			_highRange = new RangeF(0.666f, 1f);
			_midRange = new RangeF(0.333f, 0.665f);
			_lowRange = new RangeF(0f, 0.332f);
			Refresh();
		}

		public Color[] MapColors(Dictionary<int, KeyValuePair<float, float>> lChannel, Dictionary<int, KeyValuePair<float, float>> rChannel) {
			// Total number of sectors
			var len = (_hSectors + _vSectors) * 2 - 4;
			var output = new Color[len];
			_triggered = false;
			output = ColorUtil.EmptyColors(output);
			var freqRange = lChannel.Keys.ToArray();
			var freqCount = freqRange.Length;
			
			// If we have an odd number of sectors, we need to ignore the top and bottom center ones.
			if (len % 2 != 0) {
				
			}
			var secCount = len / 2; 
			foreach (var range in new[] {_lowRange, _midRange, _highRange}) {
				// Reverse order
				if (range.Min > range.Max) {
					
				} else {
					
				}
			}
			foreach (var (frequency, (hue, brightness)) in lChannel) {
				var targetHue = RotateHue(hue, _rotation);
				if (brightness >= _rotationThreshold && !_triggered) _triggered = true;
				// foreach (var (tSector, tFrequency) in map.LeftSectors) {
				// 	if (frequency == tFrequency) {
				// 		output[tSector] = ColorUtil.HsvToColor(targetHue * 360, 1, brightness);
				// 	}
				// }
			}
			
			foreach (var (frequency, (hue, brightness)) in rChannel) {
				var targetHue = RotateHue(hue, _rotation);
				if (brightness >= _rotationThreshold && !_triggered) _triggered = true;
				// foreach (var (tSector, tFrequency) in map.RightSectors) {
				// 	if (frequency == tFrequency) {
				// 		output[tSector] = ColorUtil.HsvToColor(targetHue * 360, 1, brightness);
				// 	}
				// }
			}

			// If a rotation is set, we will rotate our hue by this amount
			if (_triggered) {
				_rotation += _rotationSpeed;
				if (_rotation > 1) {
					_rotation = _rotationSpeed;
				}

				if (_rotation < 0) {
					_rotation = 1 - _rotationSpeed;
				}
			}

			return output;
		}

		private void Refresh() {
			SystemData sd = DataUtil.GetObject<SystemData>("SystemData");
			var id = sd.AudioMap;
			_hSectors = sd.HSectors;
			_vSectors = sd.VSectors;
			_rotationSpeed = sd.AudioRotationSpeed;
			_rotation = sd.AudioRotationSpeed;
			_rotationThreshold = sd.AudioRotationSensitivity;
			_rotationLower = sd.AudioRotationLower;
			_rotationUpper = sd.AudioRotationUpper;
			var len = (_hSectors + _vSectors) * 2 - 4;
			var rightStart = len - _hSectors / 2;
			var rightEnd = _vSectors + _hSectors / 2;
			if (rightStart % 2 == 0) rightStart++;
			if (rightEnd % 2 == 0) rightEnd--;
			
			_rightSectors = new List<int>();
			// Start of left range from bottom mid
			for (var i = rightStart; i < len; i++) _rightSectors.Add(i);
			// Rest of left range from 0 up to top mid
			for (var i = 0; i < rightEnd; i++ ) _rightSectors.Add(i);
			
			_leftSectors = new List<int>();
			var leftEnd = _vSectors + (_hSectors / 2 - 1);
			var leftStart = len - _hSectors / 2;
			if (leftEnd % 2 == 0) leftEnd--;
			if (leftStart % 2 == 0) leftStart++;
			Log.Debug($"LS,LE,RS,RE: {leftStart}, {leftEnd}, {rightStart}, {rightEnd}");
			for (var i = leftStart; i >= leftEnd; i--) _leftSectors.Add(i);
			Log.Debug("LeftRange: " + JsonConvert.SerializeObject(_leftSectors));
			Log.Debug("RightRange: " + JsonConvert.SerializeObject(_rightSectors));

			try {
				AudioScene am = _loader.GetItem<AudioScene>(id);
				_rotationSpeed = am.RotationSpeed;
				_rotationLower = am.RotationLower;
				_rotationUpper = am.RotationUpper;
				_highRange = am.HighRange;
				_midRange = am.MidRange;
				_lowRange = am.LowRange;
			} catch (Exception e) {
				Log.Debug("Exception updating map: " + e.Message);
			}
		}

		private float RotateHue(float hue, float rotation) {
			var output = hue + rotation;
			if (output > 1) {
				output = 1 - output;
			}

			if (output < 0) {
				output = 1 + output;
			}
			
			if (_rotationLower < _rotationUpper) {
				var range = _rotationUpper - _rotationLower;
				//.3f
				var adjusted = range * output;
				output = _rotationLower + adjusted;
			}

			return output;
		}
		
	}
}