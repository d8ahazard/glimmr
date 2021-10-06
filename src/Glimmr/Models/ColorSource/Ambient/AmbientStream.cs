#region

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Glimmr.Models.Util;
using Glimmr.Services;
using Microsoft.Extensions.Hosting;
using Newtonsoft.Json;
using Serilog;

#endregion

namespace Glimmr.Models.ColorSource.Ambient {
	public class AmbientStream : BackgroundService, IColorSource {
		private const int SectorCount = 116;
		private readonly Random _random;
		private readonly FrameSplitter _splitter;
		private readonly Stopwatch _watch;
		private string _ambientColor;
		private int _ambientShow;
		private double _animationTime;
		private FrameBuilder? _builder;
		private int _colorIndex;
		private Color[] _currentColors;
		private EasingMode _easingMode;
		private double _easingTime;
		private JsonLoader? _loader;
		private AnimationMode _mode;
		private Color[] _nextColors;
		private Color[] _sceneColors;
		private List<AmbientScene> _scenes;

		public AmbientStream(ColorService colorService) {
			_ambientColor = "#FFFFFF";
			_currentColors = Array.Empty<Color>();
			_nextColors = Array.Empty<Color>();
			_sceneColors = Array.Empty<Color>();
			_watch = new Stopwatch();
			_random = new Random();
			_loader = new JsonLoader("ambientScenes");
			_scenes = _loader.LoadFiles<AmbientScene>();
			_splitter = new FrameSplitter(colorService, false, "ambientStream");
			colorService.ControlService.RefreshSystemEvent += RefreshSystem;
		}

		public bool SourceActive => _splitter.SourceActive;


		public Task ToggleStream(CancellationToken ct) {
			Log.Debug("Starting ambient stream...");
			return ExecuteAsync(ct);
		}

		private void RefreshSystem() {
			Log.Debug("No, really, refreshing system.");
			var sd = DataUtil.GetSystemData();
			_ambientShow = sd.AmbientShow;
			_ambientColor = sd.AmbientColor;
			var dims = new[] {20, 20, 40, 40};
			_builder = new FrameBuilder(dims, true);
			_loader ??= new JsonLoader("ambientScenes");
			_scenes = _loader.LoadFiles<AmbientScene>();
			var scene = new AmbientScene();
			Log.Debug($"Loading ambient show {_ambientShow}");
			foreach (var s in _scenes.Where(s => s.Id == _ambientShow)) {
				scene = s;
				Log.Debug("Scene: " + JsonConvert.SerializeObject(s));
			}

			if (_ambientShow == -1) {
				scene.Colors = new[] {"#" + _ambientColor};
			}


			var colorStrings = scene.Colors;
			if (colorStrings != null) {
				_sceneColors = new Color[colorStrings.Length];
				for (var i = 0; i < _sceneColors.Length; i++) {
					_sceneColors[i] = ColorTranslator.FromHtml(colorStrings[i]);
				}
			} else {
				Log.Warning("Color strings are null.");
			}

			_animationTime = scene.AnimationTime * 1000f;
			_easingTime = scene.EasingTime * 1000f;
			if (scene.Easing != null) {
				_easingMode = Enum.Parse<EasingMode>(scene.Easing);
			}

			if (scene.Mode != null) {
				_mode = Enum.Parse<AnimationMode>(scene.Mode);
			} else {
				Log.Warning("Unable to parse scene mode: ");
			}

			LoadScene();
		}


		protected override Task ExecuteAsync(CancellationToken ct) {
			RefreshSystem();
			_splitter.DoSend = true;
			return Task.Run(async () => {
				// Load this one for fading
				while (!ct.IsCancellationRequested) {
					var elapsed = _watch.ElapsedMilliseconds;
					var diff = _animationTime - elapsed;
					var sectors = new Color[SectorCount];
					switch (diff) {
						// If we're between rotations, blend/fade the colors as desired
						case > 0 when diff <= _easingTime: {
							var avg = diff / _easingTime;
							for (var i = 0; i < _currentColors.Length; i++) {
								sectors[i] = _easingMode switch {
									EasingMode.Blend => BlendColor(_currentColors[i], _nextColors[i], avg),
									EasingMode.FadeIn => FadeIn(_currentColors[i], avg),
									EasingMode.FadeOut => FadeOut(_currentColors[i], avg),
									EasingMode.FadeInOut => FadeInOut(_nextColors[i], avg),
									_ => sectors[i]
								};
							}

							break;
						}
						case <= 0:
							_currentColors = _nextColors;
							_nextColors = RefreshColors(_sceneColors);
							sectors = _easingMode switch {
								EasingMode.Blend => _currentColors,
								EasingMode.FadeOut => _currentColors,
								EasingMode.FadeIn => ColorUtil.EmptyColors(SectorCount),
								EasingMode.FadeInOut => ColorUtil.EmptyColors(SectorCount),
								_ => sectors
							};
							_watch.Restart();
							break;
						default:
							sectors = _currentColors;
							break;
					}

					try {
						if (_builder == null) {
							return;
						}

						var frame = _builder.Build(sectors);
						await _splitter.Update(frame).ConfigureAwait(false);
						frame.Dispose();
					} catch (Exception e) {
						Log.Warning("EX: " + e.Message);
					}

					//await Task.Delay(TimeSpan.FromTicks(166666), CancellationToken.None);
					
				}

				_watch.Stop();
				_splitter.DoSend = false;
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


		private Color[] RefreshColors(IReadOnlyList<Color> input) {
			var output = new Color[SectorCount];
			if (input.Count == 0) {
				return ColorUtil.EmptyColors(SectorCount);
			}

			var max = input.Count - 1;
			var rand = _random.Next(0, max);
			switch (_mode) {
				case AnimationMode.Linear:
					var nu = CycleInt(_colorIndex, max);
					_colorIndex = nu;
					for (var i = 0; i < SectorCount; i++) {
						output[i] = input[nu];
						nu = CycleInt(nu, max);
					}

					break;
				case AnimationMode.Reverse:
					for (var i = 0; i < SectorCount; i++) {
						output[i] = input[_colorIndex];
						_colorIndex = CycleInt(_colorIndex, max, true);
					}

					break;
				case AnimationMode.Random:
					for (var i = 0; i < SectorCount; i++) {
						output[i] = input[rand];
						rand = _random.Next(0, max);
					}

					break;
				case AnimationMode.RandomAll:
					var col = input[rand];
					Log.Debug("Setting random color to: " + col);
					for (var i = 0; i < SectorCount; i++) {
						output[i] = col;
					}

					break;
				case AnimationMode.LinearAll:
					for (var i = 0; i < SectorCount; i++) {
						output[i] = input[_colorIndex];
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
			var output = input;
			if (reverse) {
				output--;
			} else {
				output++;
			}

			if (output > max) {
				output = 0;
			}

			if (output < 0) {
				output = max;
			}

			return output;
		}

		private void LoadScene() {
			_colorIndex = 0;
			_watch.Restart();
			// Load two arrays of colors, which we will use for the actual fade values
			_currentColors = RefreshColors(_sceneColors);
			_nextColors = RefreshColors(_sceneColors);
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