#region

using System;
using System.Diagnostics;
using System.Drawing;
using System.Threading;
using System.Threading.Tasks;
using System.Timers;
using Glimmr.Models.ColorSource.Scene.Script;
using Glimmr.Models.Frame;
using Glimmr.Models.Util;
using Glimmr.Services;
using Serilog;
using Timer = System.Timers.Timer;

#endregion

namespace Glimmr.Models.ColorSource.Scene;

/// <summary>
///     Frame-driven color source for the "Scenes" tab. Each tick it asks the active
///     <see cref="ISceneProvider" /> (a live <see cref="ParticleEngine" /> or a baked
///     <see cref="FramePlayer" />) for a Width*Height color grid, paints it onto the synthetic
///     frame via <see cref="FrameBuilder" />'s center grid, and lets <see cref="FrameSplitter" />
///     fan it out to every device type (perimeter LEDs, sectors, WLED matrix/curtains) - the same
///     path ambient matrix scenes use, so all targets and the live preview work for free.
/// </summary>
public class SceneStream : ColorSource {
	public override bool SourceActive => Splitter.SourceActive;
	public sealed override FrameBuilder? Builder { get; set; }
	public sealed override FrameSplitter Splitter { get; set; }

	private readonly SceneStore _loader;
	private readonly Timer _frameTimer;
	private ISceneProvider? _provider;
	private Color[] _grid = Array.Empty<Color>();
	private string _sceneId = "";
	private int _width = 1;
	private int _height = 1;
	private bool _mirror;
	private bool _sending;

	public SceneStream(ColorService colorService) {
		_loader = new SceneStore();
		Builder = new FrameBuilder(new[] { 1, 1, 1, 1 }, true, true);
		Splitter = new FrameSplitter(colorService);
		colorService.ControlService.RefreshSystemEvent += RefreshSystem;
		_frameTimer = new Timer(1000d / 30d);
		_frameTimer.Elapsed += UpdateFrame;
	}

	public override void RefreshSystem() {
		var started = _frameTimer.Enabled;
		if (started) {
			_frameTimer.Enabled = false;
		}

		var sd = DataUtil.GetSystemData();
		_sceneId = sd.SceneId;
		LoadScene();
		if (started) {
			_frameTimer.Enabled = true;
		}
	}

	private void LoadScene() {
		_provider = null;
		var scene = _loader.Get(_sceneId);
		if (scene == null) {
			Log.Debug($"Scene '{_sceneId}' not found.");
			return;
		}

		_mirror = scene.Mirror;
		var paramValues = SceneParams.Resolve(scene, DataUtil.GetSystemData());
		try {
			switch (scene.Kind?.ToLowerInvariant()) {
				case "video":
					_provider = new FramePlayer(scene, _loader.BlobPath(scene));
					break;
				case "color":
					_provider = new ColorProvider(ParseColor(scene.Color));
					break;
				case "image":
					_provider = new ImageProvider(scene);
					break;
				case "builtin":
					_provider = BuiltInScenes.CreateProvider(scene, paramValues);
					if (_provider == null) {
						Log.Warning($"Scene '{scene.Name}' references unknown provider '{scene.Provider}'.");
						return;
					}

					break;
				default: {
					var script = SceneParams.Apply(scene.Script, paramValues);
					var spec = SceneScriptParser.Parse(script, out var errors);
					if (errors.Count > 0) {
						Log.Warning($"Scene '{scene.Name}' script has {errors.Count} issue(s); first: {errors[0]}");
					}

					_provider = new ParticleEngine(spec);
					break;
				}
			}
		} catch (Exception e) {
			Log.Warning($"Failed to load scene '{scene.Name}': {e.Message}");
			_provider = null;
			return;
		}

		_width = Math.Max(1, _provider.Width);
		_height = Math.Max(1, _provider.Height);
		_grid = new Color[_width * _height];
		// Center grid: leftCount/rightCount = rows (height), topCount/bottomCount = cols (width).
		Builder?.Update(new[] { _height, _height, _width, _width }, true, true);
		var fps = _provider.Fps < 1 ? 30 : _provider.Fps;
		_frameTimer.Interval = 1000d / fps;
		Log.Debug($"Loaded scene '{scene.Name}' ({scene.Kind}) at {_width}x{_height}@{fps}fps.");
	}

	public override Task Start(CancellationToken ct) {
		Log.Debug("Starting scene stream...");
		RunTask = ExecuteAsync(ct);
		return Task.CompletedTask;
	}

	protected override Task ExecuteAsync(CancellationToken ct) {
		RefreshSystem();
		Splitter.DoSend = true;
		return Task.Run(async () => {
			_frameTimer.Enabled = true;
			while (!ct.IsCancellationRequested) {
				await Task.Delay(TimeSpan.FromMilliseconds(500), CancellationToken.None);
			}

			_frameTimer.Enabled = false;
			Splitter.DoSend = false;
			Log.Information("Scene stream service stopped.");
		}, CancellationToken.None);
	}

	/// <summary>
	///     Resolve a color scene's hex (falling back to the live system ambient color so the
	///     color picker can drive a solid scene).
	/// </summary>
	private static Color ParseColor(string hex) {
		if (string.IsNullOrWhiteSpace(hex)) {
			hex = DataUtil.GetSystemData().AmbientColor;
		}

		if (string.IsNullOrWhiteSpace(hex)) {
			return Color.White;
		}

		if (!hex.StartsWith('#')) {
			hex = "#" + hex;
		}

		try {
			return ColorTranslator.FromHtml(hex);
		} catch (Exception) {
			return Color.White;
		}
	}

	/// <summary>
	///     Reflect the left half of each row onto the right half (symmetric mirror).
	/// </summary>
	private static void MirrorGrid(Color[] grid, int w, int h) {
		var half = w / 2;
		for (var r = 0; r < h; r++) {
			var row = r * w;
			for (var c = 0; c < half; c++) {
				grid[row + w - 1 - c] = grid[row + c];
			}
		}
	}

	private async void UpdateFrame(object? sender, ElapsedEventArgs args) {
		if (_sending) {
			return;
		}

		_sending = true;
		try {
			var provider = _provider;
			if (provider != null && _grid.Length == _width * _height && Builder != null) {
				provider.NextFrame(_grid);
				if (_mirror) {
					MirrorGrid(_grid, _width, _height);
				}

				var frame = Builder.Build(_grid);
				if (frame != null) {
					await Splitter.Update(frame).ConfigureAwait(false);
					frame.Dispose();
				}
			}
		} catch (Exception e) {
			Log.Warning("Scene frame exception: " + e.Message);
		}

		_sending = false;
	}
}
