#region

using System;
using System.Collections.Generic;
using System.Drawing;
using Glimmr.Models.Util;
using Serilog;

#endregion

namespace Glimmr.Models.ColorSource.Audio {
	public class AudioMap {
		private readonly int[] _leftSectors;
		private readonly JsonLoader _loader;
		private readonly int[] _rightSectors;
		private float _maxVal;
		private float _minVal;
		private Dictionary<string, int> _octaveMap;
		private float _rotation;
		private float _rotationLower;
		private float _rotationSpeed;
		private float _rotationThreshold;
		private float _rotationUpper = 1;
		private bool _triggered;

		public AudioMap() {
			_octaveMap = new Dictionary<string, int>();
			_leftSectors = new[] {11, 10, 9, 8, 7, 6, 5};
			_rightSectors = new[] {12, 13, 0, 1, 2, 3, 4};
			_minVal = float.MaxValue;
			_loader = new JsonLoader("audioScenes");
			Refresh();
		}

		public IEnumerable<Color> MapColors(Dictionary<int, float> lChannel, Dictionary<int, float> rChannel) {
			// Total number of sectors
			const int len = 14;
			var output = ColorUtil.EmptyColors(len);
			var triggered = false;
			foreach (var (key, value) in _octaveMap) {
				var l = _leftSectors[value - 1];
				var r = _rightSectors[value - 1];
				var step = int.Parse(key);
				var (i, f) = HighNote(lChannel, step);
				var (key1, value1) = HighNote(rChannel, step);
				if (!triggered) {
					triggered = f >= _rotationThreshold || value1 >= _rotationThreshold;
				}

				var lHue = RotateHue(ColorUtil.HueFromFrequency(i));
				var rHue = RotateHue(ColorUtil.HueFromFrequency(key1));
				if (f > _maxVal) {
					_maxVal = f;
				}

				if (value1 > _maxVal) {
					_maxVal = value1;
				}

				if (f > 0 && f < _minVal) {
					_minVal = f;
				}

				if (value1 > 0 && value1 < _minVal) {
					_minVal = value1;
				}

				//Log.Debug($"Sector {l} using octave {step} is {lNote.Key} and {lNote.Value}");
				output[l] = ColorUtil.HsvToColor(lHue * 360, 1, f);
				output[r] = ColorUtil.HsvToColor(rHue * 360, 1, value1);
			}

			_triggered = triggered;

			return output;
		}

		private static KeyValuePair<int, float> HighNote(Dictionary<int, float> stuff, int step) {
			var minFrequency = 27.5 / 2;
			for (var i = 1; i <= step; i++) {
				minFrequency *= 2;
			}

			var amp = 0f;
			var frequency = 0;
			foreach (var (key, value) in stuff) {
				if (key < minFrequency) {
					continue;
				}

				if (key >= minFrequency * 2) {
					continue;
				}

				if (!(value > amp)) {
					continue;
				}

				amp = value;
				frequency = key;
			}

			return new KeyValuePair<int, float>(frequency, amp);
		}


		private void Refresh() {
			var sd = DataUtil.GetSystemData();
			var id = sd.AudioScene;
			var am = _loader.GetItem(id);
			_rotationSpeed = 0;
			_rotationUpper = 1;
			_rotationLower = 0;
			try {
				_rotationSpeed = am.RotationSpeed;
				_rotationLower = am.RotationLower;
				_rotationUpper = am.RotationUpper;
				_rotationThreshold = am.RotationThreshold;
				_octaveMap = am.OctaveMap;
			} catch (Exception e) {
				Log.Warning("Audio Map Refresh Exception: " + e.Message);
			}
		}

		private float RotateHue(float hue) {
			if (_triggered) {
				_triggered = false;
				_rotation += _rotationSpeed;
				if (_rotation > 1) {
					_rotation = _rotationSpeed;
				}

				if (_rotation < 0) {
					_rotation = 1 - _rotationSpeed;
				}
			}

			var output = hue;
			if (_rotationLower < _rotationUpper) {
				var range = _rotationUpper - _rotationLower;
				//.2f
				var adjusted = range * output;
				output = _rotationLower + adjusted;
			}

			output += _rotation;

			if (output > 1) {
				output = 1 - output;
			}

			if (output < 0) {
				output = 1 + output;
			}


			return output;
		}
	}
}