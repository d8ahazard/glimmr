#region

using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using Emgu.CV.Structure;
using Glimmr.Models.Util;
using Serilog;

#endregion

namespace Glimmr.Models.ColorSource.Audio {
	public class AudioMap {
		private readonly JsonLoader _loader;
		private RangeF _highRange;
		private int _hSectors;
		private List<int> _leftSectors;
		private RangeF _lowRange;
		private RangeF _midRange;
		private List<int> _rightSectors;
		private float _rotation;
		private float _rotationLower;
		private float _rotationSpeed;
		private float _rotationThreshold;
		private float _rotationUpper = 1;
		private bool _triggered;
		private int _vSectors;
		private float _maxVal;

		public AudioMap() {
			_leftSectors = new List<int>();
			_rightSectors = new List<int>();
			_loader = new JsonLoader("audioScenes");
			Refresh();
		}

		public Color[] MapColors(Dictionary<int, float> lChannel, Dictionary<int, float> rChannel) {
			// Total number of sectors
			var len = (_hSectors + _vSectors) * 2 - 4;
			var output = new Color[len];
			output = ColorUtil.EmptyColors(output);
			var triggered = false;

			foreach (var range in new[] {_lowRange, _midRange, _highRange}) {
				// Find the starting pont of our range, and the total length
				var rangeEnd = range.Max;
				var rangeStart = range.Min;
				var step = .001f;
				if (range.Min > range.Max) {
					rangeStart = rangeEnd;
					rangeEnd = range.Min;
					step = -.001f;
				}

				var lSteps = new List<int>();
				var rSteps = new List<int>();
				for (var i = rangeStart; i < rangeEnd; i += step) {
					var sector = (int) Math.Floor(_leftSectors.Count * i);
					if (!lSteps.Contains(sector)) {
						lSteps.Add(sector);
						var note = HighNote(lChannel, sector, _leftSectors.Count);
						if (note.Value >= _rotationThreshold && !triggered) {
							triggered = true;
						}

						var targetHue = RotateHue(ColorUtil.HueFromFrequency(note.Key));
						var sectorInt = _leftSectors.ElementAt(sector);
						if (note.Value > _maxVal) {
							_maxVal = note.Value;
						}

						output[sectorInt] = ColorUtil.HsvToColor(targetHue * 360, 1, note.Value);
					}

					sector = (int) Math.Floor(_rightSectors.Count * i);
					if (!rSteps.Contains(sector)) {
						rSteps.Add(sector);
						var note = HighNote(lChannel, sector, _rightSectors.Count);
						if (note.Value >= _rotationThreshold && !triggered) {
							triggered = true;
						}

						var targetHue = RotateHue(ColorUtil.HueFromFrequency(note.Key));
						var sectorInt = _rightSectors.ElementAt(sector);
						if (note.Value > _maxVal) {
							_maxVal = note.Value;
							Log.Debug("Max is " + _maxVal);
						}

						output[sectorInt] = ColorUtil.HsvToColor(targetHue * 360, 1, note.Value);
					}
				}
			}

			_triggered = triggered;

			return output;
		}

		private KeyValuePair<int, float> HighNote(Dictionary<int, float> stuff, int step, int total) {
			const int range = 15984;
			var pct = (float) step / total;
			var freq = range * pct;
			var minFrequency = OctaveFromFrequency(freq);
			var amp = 0f;
			var frequency = 0;
			foreach (var d in stuff) {
				if (d.Key < minFrequency) {
					continue;
				}

				if (d.Key >= minFrequency * 2) {
					continue;
				}

				if (d.Value > amp) {
					amp = d.Value;
					frequency = d.Key;
				}
			}

			return new KeyValuePair<int, float>(frequency, amp);
		}

		private int OctaveFromFrequency(float frequency) {
			var start = 16;
			const int max = 20000;
			while (start < max) {
				if (frequency > start) {
					start *= 2;
				} else {
					return start / 2;
				}
			}

			return max / 2;
		}

		private void Refresh() {
			var sd = DataUtil.GetSystemData();
			var id = sd.AudioMap;
			var am = _loader.GetItem(id);
			_highRange = new RangeF(0.666f, 1f);
			_midRange = new RangeF(0.333f, 0.665f);
			_lowRange = new RangeF(0f, 0.332f);
			_rotationSpeed = 0;
			_rotationUpper = 1;
			_rotationLower = 0;
			try {
				_rotationSpeed = am.RotationSpeed;
				_rotationLower = am.RotationLower;
				_rotationUpper = am.RotationUpper;
				_rotationThreshold = am.RotationThreshold;
				_highRange = am.HighRange;
				_midRange = am.MidRange;
				_lowRange = am.LowRange;
			} catch (Exception e) {
				Log.Warning("Exception: " + e.Message);
			}

			_hSectors = sd.HSectors;
			_vSectors = sd.VSectors;
			var len = (_hSectors + _vSectors) * 2 - 4;
			var rightStart = len - _hSectors / 2 + 1;
			var rightEnd = _vSectors + _hSectors / 2 - 2;

			_rightSectors = new List<int>();
			// Start of left range from bottom mid
			for (var i = rightStart; i < len; i++) {
				_rightSectors.Add(i);
			}

			// Rest of left range from 0 up to top mid
			for (var i = 0; i <= rightEnd; i++) {
				_rightSectors.Add(i);
			}

			_leftSectors = new List<int>();
			var leftEnd = _vSectors + _hSectors / 2 - 1;
			var leftStart = len - _hSectors / 2;
			for (var i = leftStart; i >= leftEnd; i--) {
				_leftSectors.Add(i);
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