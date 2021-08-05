#region

using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using Emgu.CV.Structure;
using Glimmr.Models.Util;
using Newtonsoft.Json;
using Serilog;

#endregion

namespace Glimmr.Models.ColorSource.Audio {
	public class AudioMap {
		private readonly JsonLoader _loader;
		private readonly int[] _leftSectors;
		private readonly int[] _rightSectors;
		private float _rotation;
		private float _rotationLower;
		private float _rotationSpeed;
		private float _rotationThreshold;
		private float _rotationUpper = 1;
		private bool _triggered;
		private float _maxVal;
		private float _minVal;
		private Dictionary<string, int> _octaveMap;

		public AudioMap() {
			_leftSectors = new[] {11, 10, 9, 8, 7, 6, 5};
			_rightSectors = new[] {12, 13, 0, 1, 2, 3, 4};
			_minVal = float.MaxValue;
			_loader = new JsonLoader("audioScenes");
			Refresh();
		}

		public Color[] MapColors(Dictionary<int, float> lChannel, Dictionary<int, float> rChannel) {
			// Total number of sectors
			const int len = 14;
			var output = new Color[len];
			output = ColorUtil.EmptyColors(output);
			var triggered = false;
			foreach (var (key, value) in _octaveMap) {
				var l = _leftSectors[value - 1];
				var r = _rightSectors[value - 1];
				var step = int.Parse(key);
				var lNote = HighNote(lChannel, step);
				var rNote = HighNote(rChannel, step);
				if (!triggered) {
					triggered = lNote.Value >= _rotationThreshold || rNote.Value >= _rotationThreshold;
				}
				
				var lHue = RotateHue(ColorUtil.HueFromFrequency(lNote.Key));
				var rHue = RotateHue(ColorUtil.HueFromFrequency(rNote.Key));
				if (lNote.Value > _maxVal) _maxVal = lNote.Value;
				if (rNote.Value > _maxVal) _maxVal = rNote.Value;
				if (lNote.Value > 0 && lNote.Value < _minVal) _minVal = lNote.Value;
				if (rNote.Value > 0 && rNote.Value < _minVal) _minVal = rNote.Value;
				
				//Log.Debug($"Sector {l} using octave {step} is {lNote.Key} and {lNote.Value}");
				output[l] = ColorUtil.HsvToColor(lHue * 360, 1, lNote.Value);
				output[r] = ColorUtil.HsvToColor(rHue * 360, 1, rNote.Value);
			}
			
			_triggered = triggered;

			return output;
		}

		private KeyValuePair<int, float> HighNote(Dictionary<int, float> stuff, int step) {
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
			var id = sd.AudioMap;
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
				Log.Debug("Ocatve map: " + JsonConvert.SerializeObject(_octaveMap));
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