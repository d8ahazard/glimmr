#region

using System;
using System.Collections.Generic;
using Glimmr.Models.ColorSource.Scene.Script.Providers;

#endregion

namespace Glimmr.Models.ColorSource.Scene;

/// <summary>
///     Catalog of bundled non-script animations backed by bespoke <see cref="ISceneProvider" />
///     classes (Tetris, Pac-Man, Lightning, Game of Life, the gravity box, and aurora). Each
///     entry is surfaced as a system <see cref="SceneV2" /> with <c>Kind = "builtin"</c> plus its
///     tunable params; <see cref="CreateProvider" /> instantiates the matching provider at play.
/// </summary>
public static class BuiltInScenes {
	public static List<SceneV2> All() {
		return new List<SceneV2> {
			Builtin("lightning", "Lightning", 32, 48, new List<SceneParam> {
				P("frequency", "Strike frequency", 0.1, 3, 0.1, 0.6, "How often bolts strike."),
				P("branchiness", "Branchiness", 0, 1, 0.05, 0.5, "Chance each step forks a side branch."),
				P("brightness", "Brightness", 0.3, 1.5, 0.05, 1.0)
			}),
			Builtin("tetris", "Tetris", 10, 20, new List<SceneParam> {
				P("speed", "Fall speed", 1, 30, 1, 12, "Rows per second the piece drops."),
				P("flash", "Line-clear flash", 0, 0.5, 0.02, 0.18, "Seconds cleared rows flash white.")
			}),
			Builtin("pacman", "Pac-Man", 28, 16, new List<SceneParam> {
				P("speed", "Speed", 2, 30, 1, 14, "Cells per second Pac-Man travels.")
			}),
			Builtin("gameoflife", "Game of Life", 32, 32, new List<SceneParam> {
				P("speed", "Generations/sec", 1, 30, 1, 8),
				P("density", "Seed density", 0.05, 0.9, 0.01, 0.32, "Fraction of cells alive at seed."),
				P("hue", "Base hue", 0, 360, 5, 30, "Starting color; survivors drift cooler with age.")
			}),
			Builtin("gravity", "Gravity Box", 32, 32, new List<SceneParam> {
				P("count", "Particles", 2, 120, 1, 36),
				P("duration", "Gravity switch (s)", 0.5, 10, 0.5, 3, "Max time gravity holds one set of walls."),
				P("sides", "Max walls", 1, 3, 1, 3, "How many walls can pull at once."),
				P("bounciness", "Bounciness", 0.3, 1, 0.05, 0.85),
				P("glow", "Collision glow", 0, 2, 0.1, 1.0, "Spark brightness when particles collide.")
			}),
			Builtin("aurora", "Aurora", 32, 32, new List<SceneParam> {
				P("speed", "Drift speed", 0.1, 3, 0.1, 1.0),
				P("scale", "Scale", 0.2, 3, 0.1, 1.0, "Spatial frequency of the waves."),
				P("hue", "Base hue", 0, 360, 5, 150)
			})
		};
	}

	public static ISceneProvider? CreateProvider(SceneV2 scene, IReadOnlyDictionary<string, double> values) {
		var w = Math.Max(1, scene.Width);
		var h = Math.Max(1, scene.Height);
		return scene.Provider?.ToLowerInvariant() switch {
			"lightning" => new LightningProvider(w, h, values),
			"tetris" => new TetrisProvider(w, h, values),
			"pacman" => new PacManProvider(w, h, values),
			"gameoflife" => new GameOfLifeProvider(w, h, values),
			"gravity" => new GravityBoxProvider(w, h, values),
			"aurora" => new AuroraProvider(w, h, values),
			_ => null
		};
	}

	private static SceneV2 Builtin(string id, string name, int w, int h, List<SceneParam> ps) {
		return new SceneV2 {
			Id = id, Name = name, Kind = "builtin", Provider = id,
			System = true, Width = w, Height = h, Fps = 30, Params = ps, ThumbnailFile = id + ".thumb.png"
		};
	}

	private static SceneParam P(string key, string label, double min, double max, double step,
		double def, string hint = "") {
		return new SceneParam {
			Key = key, Label = label, Min = min, Max = max, Step = step, Default = def, Hint = hint
		};
	}
}
