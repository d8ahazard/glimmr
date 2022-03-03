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
using System.Timers;
using Serilog;
using Timer = System.Timers.Timer;

#endregion

namespace Glimmr.Models.ColorSource.Ambient {
	public class AmbientStream : ColorSource {
		private const int SectorCount = 120;
		private readonly Random _random;
		private readonly FrameSplitter _splitter;
		private readonly Stopwatch _watch;
		private string _ambientColor;
		private int _ambientScene;
		private double _animationTime;
		private readonly FrameBuilder _builder;
		private int _colorIndex;
		private Color[] _currentColors;
		private EasingMode _easingMode;
		private readonly JsonLoader _loader;
		private AnimationMode _mode;
		private Color[] _nextColors;
		private Color[] _sceneColors;
		private readonly Timer _sceneTimer;
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
			_builder = new FrameBuilder(new[] { 20, 20, 40, 40 });
			_splitter = new FrameSplitter(colorService);
			colorService.ControlService.RefreshSystemEvent += RefreshSystem;
			var interval = 16.65d;
			var total = StatUtil.GetMemory(true);
			// Tempting to do this in one, but we risk DIVIDING BY ZERO.
			if (total > 0) {
				var tot = total / 1024;
				if (tot < 1024) {
					Log.Debug($"Total available RAM is lt 1024 MB ({total}), restricting frame rate.");
					interval = 33.333333333;
				}
			}
			_sceneTimer = new Timer(interval);
			_sceneTimer.Elapsed += UpdateColors;
		}

		
		public override bool SourceActive => _splitter.SourceActive;

		
		#region Data
		
		/// <summary>
		/// Refresh system data and reload scene.
		/// </summary>
		public override void RefreshSystem() {
			var sd = DataUtil.GetSystemData();
			_ambientScene = sd.AmbientScene;
			_ambientColor = sd.AmbientColor;
			LoadScene();
		}
		
		/// <summary>
		/// Load scene data to memory
		/// </summary>
		private void LoadScene() {
			// Fetch all scenes from HDD
			_scenes = _loader.LoadFiles<AmbientScene>();
			
			// Find the currently selected ambient scene's data
			var scene = new AmbientScene();
			foreach (var s in _scenes.Where(s => s.Id == _ambientScene)) {
				scene = s;
			}

			// If scene is "solid color", set it to the current ambient color.
			if (_ambientScene == -1) {
				scene.Colors = new[] { "#" + _ambientColor };
			}

			// Load our array of hex color strings to the _sceneColors variable
			var colorStrings = scene.Colors;
			if (colorStrings != null) {
				_sceneColors = new Color[colorStrings.Length];
				for (var i = 0; i < _sceneColors.Length; i++) {
					_sceneColors[i] = ColorTranslator.FromHtml(colorStrings[i]);
				}
			} else {
				Log.Warning("Color strings are null.");
			}

			// Set animation time to milliseconds
			_animationTime = scene.AnimationTime * 1000f;

			_easingMode = EasingMode.Blend;
			// Load easing mode
			if (scene.Easing != null) {
				_easingMode = Enum.Parse<EasingMode>(scene.Easing);
			}

			_mode = AnimationMode.Linear;
			// Load color mode
			if (scene.Mode != null) {
				_mode = Enum.Parse<AnimationMode>(scene.Mode);
			} else {
				Log.Warning("Unable to parse scene mode: ");
			}

			// Set color index 
			_colorIndex = 0;
			
			// Load two arrays of colors, which we will use for the actual fade values
			_currentColors = RefreshColors(_sceneColors);
			_nextColors = RefreshColors(_currentColors);
		}
		#endregion

		#region Sending

		public override Task Start(CancellationToken ct) {
			Log.Debug("Starting ambient stream...");
			RunTask = ExecuteAsync(ct);
			Log.Debug("Started...");
			return Task.CompletedTask;
		}
		
		protected override Task ExecuteAsync(CancellationToken ct) {
			RefreshSystem();
			_splitter.DoSend = true;
			return Task.Run(async () => {
				_sceneTimer.Enabled = true;
				_watch.Start();
				while (!ct.IsCancellationRequested) {
					await Task.Delay(TimeSpan.FromMilliseconds(500), CancellationToken.None);
				}
				_sceneTimer.Enabled = false;
				_watch.Stop();
				_splitter.DoSend = false;
				Log.Information("Ambient stream service stopped.");
			}, CancellationToken.None);
		}	
		
		private void UpdateColors(object? sender, ElapsedEventArgs args) {
			var sectors = new Color[SectorCount];
			// Load this one for fading
			var elapsed = _watch.ElapsedMilliseconds;
			var diff = _animationTime - elapsed;
			switch (diff) {
				// If we're between rotations, blend/fade the colors as desired
				case > 0 when diff <= _animationTime: {
					var avg = diff / _animationTime;
						
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
				_splitter.Update(frame).ConfigureAwait(false);
				frame?.Dispose();
						
						
						
			} catch (Exception e) {
				Log.Warning("EX: " + e.Message);
			}
		}

					
		#endregion

		#region ColorUtils

		

		
		private static Color BlendColor(Color target, Color dest, double percent) {
			var r1 = (int) ((target.R - dest.R) * percent) + dest.R;
			var g1 = (int) ((target.G - dest.G) * percent) + dest.G;
		var b1 = (int)((target.B - dest.B) * percent) + dest.B;
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
				Log.Warning("Unknown animation mode: " + _mode);
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

	#endregion

		#region Enumerators


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
	#endregion
	}

}