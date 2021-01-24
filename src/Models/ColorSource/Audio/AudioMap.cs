using System;
using System.Collections.Generic;
using System.Drawing;
using Glimmr.Models.ColorSource.Audio.Map;
using Glimmr.Models.Util;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Serilog;

namespace Glimmr.Models.ColorSource.Audio {
	public class AudioMap {
		public enum MapType {
			Bottom,
			Top,
			Center,
			Middle,
			Corners
		}

		private float _rotationSpeed;
		private float _rotation;
		private float _rotationThreshold;
		private float _rotationUpper = 1;
		private float _rotationLower;
		
		private bool triggered;
		
		private IAudioMap map;

		public AudioMap(MapType type, float speed = 0) {
			if (speed >= 1.0f || speed <= -1.0f) {
				throw new ArgumentException("Invalid rotation speed, value must be within -1 and 1.");
			}
			UpdateMap(type);
		}

		public Color[] MapColors(Dictionary<int, KeyValuePair<float, float>> lChannel, Dictionary<int, KeyValuePair<float, float>> rChannel, int len) {
			var output = new Color[len];
			triggered = false;
			output = ColorUtil.EmptyColors(output);
			foreach (var (frequency, (hue, brightness)) in lChannel) {
				var targetHue = RotateHue(hue, _rotation);
				if (brightness >= _rotationThreshold && !triggered) triggered = true;
				foreach (var (tSector, tFrequency) in map.LeftSectors) {
					if (frequency == tFrequency) {
						output[tSector] = ColorUtil.HsvToColor(targetHue * 360, 1, brightness);
					}
				}
			}
			
			foreach (var (frequency, (hue, brightness)) in rChannel) {
				var targetHue = RotateHue(hue, _rotation);
				if (brightness >= _rotationThreshold && !triggered) triggered = true;
				foreach (var (tSector, tFrequency) in map.RightSectors) {
					if (frequency == tFrequency) {
						output[tSector] = ColorUtil.HsvToColor(targetHue * 360, 1, brightness);
					}
				}
			}

			// If a rotation is set, we will rotate our hue by this amount
			if (triggered) {
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

		public void UpdateMap(MapType type) {
			switch (type) {
				case MapType.Bottom:
					map = new AudioMapBottom();
					break;
				case MapType.Top:
					map = new AudioMapTop();
					break;
				case MapType.Middle:
					map = new AudioMapMiddle();
					break;
				case MapType.Corners:
					map = new AudioMapCorners();
					break;
				case MapType.Center:
					map = new AudioMapCenter();
					break;
				default:
					map = new AudioMapBottom();
					break;
			}

			SystemData sd = DataUtil.GetObject<SystemData>("SystemData");
			_rotationSpeed = sd.AudioRotationSpeed;
			_rotation = sd.AudioRotationSpeed;
			_rotationThreshold = sd.AudioRotationSensitivity;
			_rotationLower = sd.AudioRotationLower;
			_rotationUpper = sd.AudioRotationUpper;
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