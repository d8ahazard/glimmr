using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Glimmr.Enums;
using Glimmr.Models.Util;
using Glimmr.Services;
using Microsoft.Extensions.Hosting;
using Q42.HueApi.Streaming.Models;
using Serilog;

namespace Glimmr.Models.ColorSource.Ambient {
	public class AmbientStream : BackgroundService, IColorSource {
		private Color[] _ledColors;
		private Color[] _sectorColors;
		private readonly ColorService _cs;
		private readonly Random _random;
		private readonly Stopwatch _watch;
		private string _ambientColor;
		private int _ambientShow;
		private double _animationTime;
		private int _colorIndex;
		private string[] _colors;
		private Color[] _currentColors;
		private EasingMode _easingMode;
		private double _easingTime;

		private bool _enable;
		private int _ledCount;
		private int _hCount;
		private int _vCount;
		private JsonLoader _loader;
		private AnimationMode _mode;
		private Color[] _nextColors;
		private List<AmbientScene> _scenes;
		private int _sectorCount;

		public AmbientStream(ColorService colorService) {
			_watch = new Stopwatch();
			_random = new Random();
			_cs = colorService;
			_cs.AddStream(DeviceMode.Ambient, this);
		}

		public void ToggleStream(bool enable = false) {
			_enable = enable;
		}

		private void LoadSystem() {
			var sd = DataUtil.GetSystemData();
			Refresh(sd);
		}

		public void Refresh(SystemData sd) {
			_sectorCount = sd.HSectors + sd.VSectors + sd.HSectors + sd.VSectors - 4;
			_ledCount = sd.LedCount;
			_ambientShow = sd.AmbientShow;
			_ambientColor = sd.AmbientColor;
			_hCount = sd.HSectors;
			_vCount = sd.VSectors;
			_loader = new JsonLoader("ambientScenes");
			_scenes = _loader.LoadFiles<AmbientScene>();
			var scene = new AmbientScene();
			foreach (var s in _scenes.Where(s => s.Id == _ambientShow)) {
				scene = s;
			}

			if (_ambientShow == -1) {
				scene.Colors = new[] {"#" + _ambientColor};
			}

			_colors = scene.Colors;
			_animationTime = scene.AnimationTime * 1000;
			_easingTime = scene.EasingTime * 1000;
			_easingMode = (EasingMode) scene.Easing;
			_mode = (AnimationMode) scene.Mode;
			
			LoadScene();
		}

		public bool SourceActive { get; set; }

		protected override Task ExecuteAsync(CancellationToken ct) {
			Log.Debug("Starting ambient stream service...");
			LoadSystem();
			return Task.Run(async () => {
				// Load this one for fading
				while (!ct.IsCancellationRequested) {
					if (!_enable) {
						continue;
					}

					var elapsed = _watch.ElapsedMilliseconds;
					var diff = _animationTime - elapsed;
					var sectors = new Color[_sectorCount];
					// If we're between rotations, blend/fade the colors as desired
					if (diff > 0 && diff <= _easingTime) {
						var avg = diff / _easingTime;
						for (var i = 0; i < _currentColors.Length; i++) {
							switch (_easingMode) {
								case EasingMode.Blend:
									sectors[i] = BlendColor(_currentColors[i], _nextColors[i], avg);
									break;
								case EasingMode.FadeIn:
									sectors[i] = FadeIn(_currentColors[i], avg);
									break;
								case EasingMode.FadeOut:
									sectors[i] = FadeOut(_currentColors[i], avg);
									break;
								case EasingMode.FadeInOut:
									sectors[i] = FadeInOut(_nextColors[i], avg);
									break;
							}
						}
						// If our time has elapsed, restart the watch 
					} else if (diff <= 0) {
						switch (_easingMode) {
							case EasingMode.Blend:
							case EasingMode.FadeOut:
								_currentColors = _nextColors;
								_nextColors = RefreshColors(_colors);
								sectors = _currentColors;
								break;
							case EasingMode.FadeIn:
							case EasingMode.FadeInOut:
								_currentColors = _nextColors;
								_nextColors = RefreshColors(_colors);
								sectors = ColorUtil.EmptyColors(sectors);
								break;
						}
						_watch.Restart();
					} else {
						sectors = _currentColors;
					}

					_ledColors = ColorUtil.SectorsToleds(sectors.ToList(), _hCount, _vCount).ToArray();
					_sectorColors = sectors;
					_cs.SendColors(_ledColors.ToList(), _sectorColors.ToList(), 0);
					await Task.FromResult(true);
				}

				_watch.Stop();
				Log.Information("Ambient stream service stopped.");
			}, CancellationToken.None);
		}


		private static Color BlendColor(Color target, Color dest, double percent) {
			var r1 = (int) ((target.R - dest.R) * percent) + dest.R;
			var g1 = (int) ((target.G - dest.G) * percent) + dest.G;
			var b1 = (int) ((target.B - dest.B) * percent) + dest.B;
			r1 = r1 > 255 ? 255 : r1 < 0 ? 0 : r1;
			g1 = g1 > 255 ? 255 : g1 < 0 ? 0 : g1;
			b1 = b1 > 255 ? 255 : b1 < 0 ? 0 : b1;
			return Color.FromArgb(255, r1, g1, b1);
		}

		private static Color FadeOut(Color target, double percent) {
			return BlendColor(target, Color.FromArgb(255, 0, 0, 0), percent);
		}

		private static Color FadeIn(Color target, double percent) {
			return BlendColor(Color.FromArgb(255, 0, 0, 0), target, percent);
		}

		private static Color FadeInOut(Color target, double percent) {
			if (percent <= .5) {
				return FadeOut(target, percent * 2);
			}

			var pct = (percent - .5) * 2;
			return FadeIn(target, pct);
		}


		private Color[] RefreshColors(string[] input) {
			var output = new Color[_sectorCount];
			if (input == null) {
				return ColorUtil.EmptyColors(output);
			}

			var max = input.Length;
			var rand = _random.Next(0, max);
			switch (_mode) {
				case AnimationMode.Linear:
					for (var i = 0; i < _sectorCount; i++) {
						output[i] = ColorTranslator.FromHtml(input[_colorIndex]);
						_colorIndex = CycleInt(_colorIndex, max);
					}
					_colorIndex = CycleInt(_colorIndex, max);
					break;
				case AnimationMode.Reverse:
					for (var i = 0; i < _sectorCount; i++) {
						output[i] = ColorTranslator.FromHtml(input[_colorIndex]);
						_colorIndex = CycleInt(_colorIndex, max, true);
					}

					break;
				case AnimationMode.Random:
					for (var i = 0; i < _sectorCount; i++) {
						output[i] = ColorTranslator.FromHtml(input[rand]);
						rand = _random.Next(0, max);
					}

					break;
				case AnimationMode.RandomAll:
					for (var i = 0; i < _sectorCount; i++) {
						output[i] = ColorTranslator.FromHtml(input[rand]);
					}

					break;
				case AnimationMode.LinearAll:
					for (var i = 0; i < _sectorCount; i++) {
						output[i] = ColorTranslator.FromHtml(input[_colorIndex]);
					}

					_colorIndex = CycleInt(_colorIndex, max);
					break;
				default:
					Log.Debug("Unknown animation mode: " + _mode);
					break;
			}

			return output;
		}

		private static int CycleInt(int input, int max, bool reverse = false) {
			if (reverse) {
				input--;
			} else {
				input++;
			}

			if (input >= max) {
				input = 0;
			}

			if (input < 0) {
				input = max;
			}

			return input;
		}

		private void LoadScene() {
			_colorIndex = 0;
			_watch.Restart();
			// Load two arrays of colors, which we will use for the actual fade values
			_currentColors = RefreshColors(_colors);
			_nextColors = RefreshColors(_colors);
			Log.Debug($"Loaded, color len is {_currentColors.Length}");
		}


        /// <summary>
        ///     Linear - Each color from the list of colors is assigned to a sector, and the order is incremented by 1 each update
        ///     Reverse - Same as linear, but the order is decremented each update
        ///     Random - A random color will be assigned to each sector every update
        ///     RandomAll - One random color will be selected and applied to all sectors each update
        ///     LinearAll - One color will be selected and applied to all tiles, with the color incremented each update
        /// </summary>
        private enum AnimationMode {
			Linear = 0,
			Reverse = 1,
			Random = 2,
			RandomAll = 3,
			LinearAll = 4
		}

		private enum EasingMode {
			Blend = 0,
			FadeIn = 1,
			FadeOut = 2,
			FadeInOut = 3
		}
	}
}