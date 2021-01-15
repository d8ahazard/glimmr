using System;
using System.Collections.Generic;
using System.Drawing;
using Glimmr.Models.ColorSource.Audio.Map;
using Glimmr.Models.Util;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Glimmr.Models.ColorSource.Audio {
	public class AudioMap {
		public enum MapType {
			Bottom,
			Top,
			Center,
			Middle,
			Corners,
			Cool,
			Warm,
			Rotating
		}

		public float RotationSpeed;
		private float _rotation;

		private IAudioMap map;

		public AudioMap(MapType type, float speed = 0) {
			if (speed >= 1.0f || speed <= -1.0f) {
				throw new ArgumentException("Invalid rotation speed, value must be within -1 and 1.");
			}
			UpdateMap(type);
			RotationSpeed = speed;
		}

		public Color[] MapColors(Dictionary<int, KeyValuePair<float, float>> lChannel, Dictionary<int, KeyValuePair<float, float>> rChannel, int len) {
			var output = new Color[len];
			output = ColorUtil.EmptyColors(output);
			foreach (var l in lChannel) {
				var sector = map.LeftSectors[l.Key];
				var hue = RotateHue(l.Value.Key, _rotation);
				var brightness = l.Value.Value;
				output[sector] = ColorUtil.HsbToColor(hue, 1, brightness);
			}
			foreach (var r in rChannel) {
				var sector = map.LeftSectors[r.Key];
				var hue = RotateHue(r.Value.Key, _rotation);
				var brightness = r.Value.Value;
				output[sector] = ColorUtil.HsbToColor(hue, 1, brightness);
			}

			// If a rotation is set, we will rotate our hue by this amount
			_rotation += RotationSpeed;
			if (_rotation > 1) {
				_rotation -= 1;
			}

			if (_rotation < 0) {
				_rotation = 1 + _rotation;
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
			}

			RotationSpeed = map.RotationSpeed;
		}

		private static float RotateHue(float hue, float rotation) {
			var output = hue + rotation;
			if (output > 1) {
				return 1 - output;
			}

			if (output < 0) {
				return 1 + output;
			}

			return output;
		}
		
	}
}