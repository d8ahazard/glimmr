#region

using System;
using System.Collections.Generic;
using System.Drawing;
using Glimmr.Models.Helpers;
using Glimmr.Models.Util;
using Serilog;

#endregion

namespace Glimmr.Models.ColorSource.Audio;

public class AudioMap {
	private readonly int[] _leftSectors;
	private readonly JsonLoader _loader;
	private readonly int[] _rightSectors;
	private int _kickCount;
	private float _maxVal;
	private float _minVal;
	private Dictionary<string, int> _octaveMap;
	private float _rotation;
	private float _rotationLower;
	private float _rotationSpeed;
	private int _rotationThreshold;
	private float _rotationUpper = 1f;
	private bool _triggered;

	public AudioMap() {
		_octaveMap = new Dictionary<string, int>();
		_leftSectors = new[] { 16, 15, 14, 13, 12, 11, 10, 9, 8, 7 };
		_rightSectors = new[] { 17, 18, 19, 0, 1, 2, 3, 4, 5, 6 };
		_minVal = float.MaxValue;
		_loader = new JsonLoader("audioScenes");
		Refresh();
	}

	public IEnumerable<Color> MapColors(Dictionary<float, int> lChannel) {
		// Total number of sectors
		const int len = 20;
		var output = ColorUtil.EmptyColors(len);
		if (lChannel.Count == 0) {
			return output;
		}

		var triggered = false;
		var l = 0;
		var r = 0;
		var black = Color.FromArgb(0, 0, 0, 0);

		//Log.Debug("LMap: " + JsonConvert.SerializeObject(lChannel));
		foreach (var (key, octave) in _octaveMap) {
			try {
				var region = int.Parse(key);
				l = _leftSectors[region];
				r = _rightSectors[region];
				var (lFreq, lMax) = HighNote(lChannel, octave);
				if (lMax == 0) {
					output[l] = black;
					output[r] = black;
				} else {
					if (!triggered && lFreq < 100) {
						triggered = lMax >= _rotationThreshold;
					}

					//Log.Debug($"MaxFreq {octave} is {lFreq} at {lMax}");
					var lHue = RotateHue(ColorUtil.HueFromFrequency(lFreq, octave));
					if (lMax > _maxVal) {
						_maxVal = lMax;
					}

					if (lMax > 0 && lMax < _minVal) {
						_minVal = lMax;
					}

					output[l] = ColorUtil.HsvToColor(lHue * 360, 1, lMax / 255f);
					output[r] = output[l];
				}
			} catch (Exception e) {
				Log.Debug($"Ex {l} {r}: " + e.Message + " at " + e.StackTrace);
			}
		}

		if (triggered) {
			_kickCount++;
		}

		if (_kickCount >= 4) {
			_kickCount = 0;
			_triggered = true;
		}

		return output;
	}

	/// <summary>
	///     Select the highest frequency in a given octave, where step is the octave from 0-9
	/// </summary>
	/// <param name="stuff"></param>
	/// <param name="step"></param>
	/// <returns></returns>
	private static KeyValuePair<float, int> HighNote(Dictionary<float, int> stuff, int step) {
		//Log.Debug($"Octave range {step} is {low} to {high}");
		var start = new[] { 16.35f, 32.7f, 65.41f, 130.81f, 261.63f, 523.25f, 1046.5f, 2093f, 4186.01f };
		var end = new[] { 30.87f, 61.74f, 123.47f, 246.94f, 493.88f, 987.77f, 1975.53f, 3951.07f, 7902.13f };
		var minFrequency = start[step];
		var maxFrequency = end[step];
		var amp = 0;
		var frequency = 0f;
		foreach (var (key, value) in stuff) {
			if (key < minFrequency) {
				continue;
			}

			if (key > maxFrequency) {
				continue;
			}

			if (!(value > amp)) {
				continue;
			}

			amp = value;
			frequency = key;
		}

		return new KeyValuePair<float, int>(frequency, amp);
	}


	private void Refresh() {
		var sd = DataUtil.GetSystemData();
		var id = sd.AudioScene;
		var am = _loader.GetItem(id);
		_rotationSpeed = sd.AudioRotationSpeed;
		_rotationLower = sd.AudioRotationLower;
		_rotationUpper = sd.AudioRotationUpper;
		_rotationThreshold = sd.AudioRotationTrigger;
		try {
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
		var range = Math.Abs(_rotationUpper - _rotationLower);
		output = _rotationLower + range * output;
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