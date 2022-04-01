#region

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Timers;
using Glimmr.Models.Frame;
using Glimmr.Models.Helper;
using Glimmr.Models.Util;
using Glimmr.Services;
using Serilog;
using Timer = System.Timers.Timer;

#endregion

namespace Glimmr.Models.ColorSource.Ambient;

public class AmbientStream : ColorSource {
	public override bool SourceActive => Splitter.SourceActive;
	public sealed override FrameBuilder? Builder { get; set; }
	public sealed override FrameSplitter Splitter { get; set; }
	private readonly JsonLoader _loader;
	private readonly Random _random;
	private readonly Timer _sceneTimer;

	private readonly Stopwatch _watch;
	private string _ambientColor;
	private int _ambientScene;
	private double _animationTime;
	private int _colorIndex;
	private ColorMatrix? _colorMatrix;
	private List<Color[]> _colors;
	private EasingMode _easingMode;
	private Color[] _emptyColors;
	private AnimationMode _mode;
	private Color[] _sceneColors;
	private List<AmbientScene> _scenes;
	private int _sectorCount = 120;
	private bool _sending;

	public AmbientStream(ColorService colorService) {
		_colors = new List<Color[]>();
		_ambientColor = "#FFFFFF";
		_emptyColors = ColorUtil.EmptyColors(_sectorCount);
		_sceneColors = Array.Empty<Color>();
		_watch = new Stopwatch();
		_random = new Random();
		_loader = new JsonLoader("ambientScenes");
		_scenes = _loader.LoadFiles<AmbientScene>();
		Builder = new FrameBuilder(new[] { 20, 20, 40, 40 });
		Splitter = new FrameSplitter(colorService);
		colorService.ControlService.RefreshSystemEvent += RefreshSystem;
		var fps = 16.65d;
		var total = StatUtil.GetMemory(true);
		// Tempting to do this in one, but we risk DIVIDING BY ZERO.
		if (total > 0) {
			var tot = total / 1024;
			if (tot < 1024) {
				Log.Debug($"Total available RAM is lt 1024 MB ({total}), restricting frame rate.");
				fps = 33.333333333;
			}
		}

		_sceneTimer = new Timer(fps);
		_sceneTimer.Elapsed += UpdateColors;
	}


	#region Data

	/// <summary>
	///     Refresh system data and reload scene.
	/// </summary>
	public override void RefreshSystem() {
		var started = _sceneTimer.Enabled;
		if (started) {
			_sceneTimer.Enabled = false;
		}

		var sd = DataUtil.GetSystemData();
		_ambientScene = sd.AmbientScene;
		_ambientColor = sd.AmbientColor;
		LoadScene();
		if (started) {
			_sceneTimer.Enabled = true;
		}
	}

	/// <summary>
	///     Load scene data to memory
	/// </summary>
	private void LoadScene() {
		// Fetch all scenes from HDD
		_scenes = _loader.LoadFiles<AmbientScene>();
		_colors = new List<Color[]>();

		// Find the currently selected ambient scene's data
		var scene = new AmbientScene();
		foreach (var s in _scenes.Where(s => s.Id == _ambientScene)) {
			scene = s;
		}

		Log.Debug($"Loading ambient scene '{scene.Name}'.");
		// If scene is "solid color", set it to the current ambient color.
		if (_ambientScene == -1) {
			scene.Colors = new[] { "#" + _ambientColor };
		}

		// Load our array of hex color strings to the _sceneColors variable
		var colorStrings = scene.Colors ?? Array.Empty<string>();
		_sceneColors = new Color[colorStrings.Length];
		for (var i = 0; i < _sceneColors.Length; i++) {
			_sceneColors[i] = ColorTranslator.FromHtml(colorStrings[i]);
		}

		// Set animation time to milliseconds
		_animationTime = scene.AnimationTime * 1000f;

		// Load easing mode
		_easingMode = EasingMode.Blend;
		_easingMode = Enum.Parse<EasingMode>(scene.Easing);

		_mode = AnimationMode.Linear;
		_mode = Enum.Parse<AnimationMode>(scene.Mode);
		Log.Debug("Disposing builder...");

		if (_mode == AnimationMode.Matrix) {
			Log.Debug("Loading matrix.");
			_colorMatrix = new ColorMatrix(scene);
			_sectorCount = _colorMatrix.Size;
			_emptyColors = ColorUtil.EmptyColors(_sectorCount).ToArray();
			var w = _colorMatrix.Width;
			var h = _colorMatrix.Height;
			Builder?.Update(new[] { h, h, w, w }, true, true);

			_colorMatrix.Update();
			_colors.Add(_colorMatrix.ColorArray().ToArray());
			_colorMatrix.Update();
			_colors.Add(_colorMatrix.ColorArray().ToArray());
			Log.Debug("R1");
			_colorIndex = 0;
			Log.Debug($"Matrix loaded, dims are {w} x {h}");
		} else {
			_colorMatrix = null;
			_sectorCount = 120;
			Log.Debug("Creating standard builder...");
			_emptyColors = ColorUtil.EmptyColors(_sectorCount).ToArray();
			Builder?.Update(new[] { 20, 20, 40, 40 });
			// Load two arrays of colors, which we will use for the actual fade values
			_colors.Add(RefreshColors(_sceneColors).ToArray());
			_colors.Add(RefreshColors(_colors[0]).ToArray());
		}

		// Set color index 
		_colorIndex = 0;
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
		Splitter.DoSend = true;
		return Task.Run(async () => {
			_sceneTimer.Enabled = true;
			_watch.Restart();
			while (!ct.IsCancellationRequested) {
				await Task.Delay(TimeSpan.FromMilliseconds(500), CancellationToken.None);
			}

			_sceneTimer.Enabled = false;
			_watch.Stop();
			Splitter.DoSend = false;
			Log.Information("Ambient stream service stopped.");
		}, CancellationToken.None);
	}

	private void UpdateColors(object? sender, ElapsedEventArgs args) {
		if (_sending) {
			return;
		}

		_sending = true;
		var sectors = _emptyColors;
		// Load this one for fading
		var elapsed = _watch.ElapsedMilliseconds;
		var diff = _animationTime - elapsed;
		switch (diff) {
			// If we're between rotations, blend/fade the colors as desired
			case > 0 when diff <= _animationTime: {
				var avg = diff / _animationTime;
				sectors = _easingMode switch {
					EasingMode.Blend => BlendColor(_colors[0], _colors[1], avg),
					EasingMode.FadeIn => FadeIn(_colors[0], avg),
					EasingMode.FadeOut => FadeOut(_colors[0], avg),
					EasingMode.FadeInOut => FadeInOut(_colors[1], avg),
					_ => sectors
				};
				break;
			}
			case <= 0:
				_colors[0] = _colors[1].ToArray();
				_colors[1] = RefreshColors(_sceneColors);
				var output = _easingMode switch {
					EasingMode.Blend => _colors[0],
					EasingMode.FadeOut => _colors[0],
					EasingMode.FadeIn => _emptyColors,
					EasingMode.FadeInOut => _emptyColors,
					_ => sectors
				};
				sectors = output.ToArray();
				_watch.Restart();
				break;
			default:
				sectors = _colors[0];
				break;
		}


		try {
			if (Builder != null) {
				var frame = Builder.Build(sectors);
				Splitter.Update(frame).ConfigureAwait(true);
				frame?.Dispose();
			}
		} catch (Exception e) {
			Log.Warning("EX: " + e.Message + " at " + e.StackTrace);
		}

		_sending = false;
	}

	#endregion

	#region ColorUtils

	private static Color[] BlendColor(IReadOnlyList<Color> targets, IReadOnlyList<Color> destinations, double percent) {
		var output = new Color[targets.Count];
		for (var t = 0; t < targets.Count; t++) {
			var target = targets[t];
			var dest = destinations[t];
			var r = (int)((target.R - dest.R) * percent) + dest.R;
			var g = (int)((target.G - dest.G) * percent) + dest.G;
			var b = (int)((target.B - dest.B) * percent) + dest.B;
			r = r > 255 ? 255 : r < 0 ? 0 : r;
			g = g > 255 ? 255 : g < 0 ? 0 : g;
			b = b > 255 ? 255 : b < 0 ? 0 : b;
			output[t] = Color.FromArgb(r, g, b);
		}

		return output;
	}

	private Color[] FadeOut(IReadOnlyList<Color> target, double percent) {
		return BlendColor(target, _emptyColors, percent);
	}

	private Color[] FadeIn(IReadOnlyList<Color> target, double percent) {
		return BlendColor(_emptyColors, target, percent);
	}

	private Color[] FadeInOut(IReadOnlyList<Color> target, double percent) {
		if (percent <= .5) {
			return FadeOut(target, percent * 2);
		}

		var pct = (percent - .5) * 2;
		return FadeIn(target, pct);
	}

	private Color[] RefreshColors(IReadOnlyList<Color> input) {
		var output = new Color[_sectorCount];
		if (input.Count == 0 && _mode != AnimationMode.Matrix) {
			Log.Debug("No input and not matrix?");
			return ColorUtil.EmptyColors(_sectorCount);
		}

		var max = input.Count == 0 ? 0 : input.Count - 1;
		var rand = _random.Next(0, max);
		switch (_mode) {
			case AnimationMode.Linear:
				var nu = CycleInt(_colorIndex, max);
				_colorIndex = nu;
				for (var i = 0; i < _sectorCount; i++) {
					output[i] = input[nu];
					nu = CycleInt(nu, max);
				}

				break;
			case AnimationMode.Reverse:
				for (var i = 0; i < _sectorCount; i++) {
					output[i] = input[_colorIndex];
					_colorIndex = CycleInt(_colorIndex, max, true);
				}

				break;
			case AnimationMode.Random:
				for (var i = 0; i < _sectorCount; i++) {
					output[i] = input[rand];
					rand = _random.Next(0, max);
				}

				break;
			case AnimationMode.RandomAll:
				var col = input[rand];
				for (var i = 0; i < _sectorCount; i++) {
					output[i] = col;
				}

				break;
			case AnimationMode.LinearAll:
				for (var i = 0; i < _sectorCount; i++) {
					output[i] = input[_colorIndex];
				}

				_colorIndex = CycleInt(_colorIndex, max);
				break;
			case AnimationMode.Matrix:
				if (_colorMatrix == null) {
					return output;
				}

				_colorMatrix.Update();
				output = _colorMatrix.ColorArray().ToArray();
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
	///     Matrix - Uses a 9 x 16 grid to create more advanced animations. Map must include a
	/// </summary>
	private enum AnimationMode {
		Linear = 0,
		Reverse = 1,
		Random = 2,
		RandomAll = 3,
		LinearAll = 4,
		Matrix = 5
	}

	/// <summary>
	///     The direction the matrix will move in.
	/// </summary>
	public enum MatrixDirection {
		/// <summary>
		///     Direction changes each frame cycle
		/// </summary>
		Random = 0,

		/// <summary>
		///     Left-to-right
		/// </summary>
		LTR = 1,

		/// <summary>
		///     Top-to-bottom
		/// </summary>
		TTB = 2,

		/// <summary>
		///     Right-to-left
		/// </summary>
		RTL = 3,

		/// <summary>
		///     Bottom to top
		/// </summary>
		BTT = 4,

		/// <summary>
		///     Rotate clockwise
		/// </summary>
		CW = 5,

		/// <summary>
		///     Rotate counter-clockwise
		/// </summary>
		CCW = 6
	}

	private enum EasingMode {
		Blend = 0,
		FadeIn = 1,
		FadeOut = 2,
		FadeInOut = 3
	}

	#endregion
}