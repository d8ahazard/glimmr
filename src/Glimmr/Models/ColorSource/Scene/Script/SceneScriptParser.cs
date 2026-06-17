#region

using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.Linq;

#endregion

namespace Glimmr.Models.ColorSource.Scene.Script;

/// <summary>
///     Parses the Glimmr animation shorthand into a <see cref="ParticleSceneSpec" />.
///     The language is line based:
///     <code>
///     # full line comment
///     @scene fps=30 bg=#000010 trail=0.85 size=32x48
///
///     emit rockets
///       rate    = 1 per 0.5..1.0s     // continuous: 1 particle every 0.5-1.0s
///       origin  = x:rand y:bottom
///       vel     = dir:up speed:30..40
///       gravity = down 20
///       color   = #ffcc55
///       die     = when vy&lt;=0        // die at the apex
///       spawn   = burst               // spawn the 'burst' emitter on death
///
///     emit burst
///       count   = 50..90 burst        // emitted all at once when spawned
///       origin  = parent
///       vel     = dir:radial speed:12..28
///       gravity = down 14
///       color   = pick(#ff3b3b,#ffd23b,#3bff6b)
///       fade    = 1.2s
///     </code>
///     Inline comments start with <c>//</c> (so <c>#</c> stays free for hex colors).
/// </summary>
public static class SceneScriptParser {
	public static ParticleSceneSpec Parse(string script, out List<string> errors) {
		errors = new List<string>();
		var spec = new ParticleSceneSpec();
		EmitterSpec? current = null;
		var lines = (script ?? string.Empty).Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');

		for (var i = 0; i < lines.Length; i++) {
			var raw = StripComment(lines[i]).Trim();
			if (raw.Length == 0) {
				continue;
			}

			var lineNo = i + 1;
			try {
				if (raw.StartsWith("@scene", StringComparison.OrdinalIgnoreCase)) {
					ParseScene(raw[6..].Trim(), spec, errors, lineNo);
					current = null;
					continue;
				}

				if (raw.StartsWith("emit", StringComparison.OrdinalIgnoreCase) &&
				    (raw.Length == 4 || char.IsWhiteSpace(raw[4]))) {
					var name = raw[4..].Trim();
					if (name.Length == 0) {
						errors.Add($"Line {lineNo}: 'emit' needs a name.");
						continue;
					}

					current = new EmitterSpec { Name = name };
					spec.Emitters[name] = current;
					continue;
				}

				// Otherwise it's a key = value property of the current emitter.
				var eq = raw.IndexOf('=');
				if (eq < 0) {
					errors.Add($"Line {lineNo}: expected 'key = value' (got '{raw}').");
					continue;
				}

				if (current == null) {
					errors.Add($"Line {lineNo}: property '{raw}' outside of any 'emit' block.");
					continue;
				}

				var key = raw[..eq].Trim().ToLowerInvariant();
				var value = raw[(eq + 1)..].Trim();
				ParseProperty(current, key, value, errors, lineNo);
			} catch (Exception e) {
				errors.Add($"Line {lineNo}: {e.Message}");
			}
		}

		// Resolve root emitters + validate spawn references.
		foreach (var em in spec.Emitters.Values) {
			if (em.Continuous) {
				spec.RootEmitters.Add(em.Name);
			}

			if (em.SpawnOnDeath != null && !spec.Emitters.ContainsKey(em.SpawnOnDeath)) {
				errors.Add($"Emitter '{em.Name}' spawns unknown emitter '{em.SpawnOnDeath}'.");
			}
		}

		if (spec.Emitters.Count == 0) {
			errors.Add("Script defines no emitters.");
		} else if (spec.RootEmitters.Count == 0) {
			errors.Add("No root emitter: at least one emitter needs a 'rate' to spawn particles.");
		}

		return spec;
	}

	private static string StripComment(string line) {
		var idx = line.IndexOf("//", StringComparison.Ordinal);
		if (idx >= 0) {
			line = line[..idx];
		}

		// Full-line '#' comments only (so inline hex colors survive).
		return line.TrimStart().StartsWith('#') ? string.Empty : line;
	}

	private static void ParseScene(string body, ParticleSceneSpec spec, List<string> errors, int lineNo) {
		foreach (var tok in body.Split(' ', StringSplitOptions.RemoveEmptyEntries)) {
			var kv = tok.Split('=', 2);
			if (kv.Length != 2) {
				continue;
			}

			var k = kv[0].Trim().ToLowerInvariant();
			var v = kv[1].Trim();
			switch (k) {
				case "fps":
					spec.Fps = Math.Clamp(ParseDouble(v), 1, 60);
					break;
				case "bg":
				case "background":
					spec.Background = ParseColor(v);
					break;
				case "trail":
					spec.Trail = Math.Clamp(ParseDouble(v), 0, 0.999);
					break;
				case "size":
					var dims = v.ToLowerInvariant().Split('x');
					if (dims.Length == 2) {
						spec.Width = Math.Clamp((int)ParseDouble(dims[0]), 1, 128);
						spec.Height = Math.Clamp((int)ParseDouble(dims[1]), 1, 128);
					} else {
						errors.Add($"Line {lineNo}: size must look like 32x48.");
					}

					break;
				default:
					errors.Add($"Line {lineNo}: unknown @scene option '{k}'.");
					break;
			}
		}
	}

	private static void ParseProperty(EmitterSpec em, string key, string value, List<string> errors, int lineNo) {
		var tokens = value.Split(' ', StringSplitOptions.RemoveEmptyEntries);
		switch (key) {
			case "rate":
				// e.g. "1 per 0.5..1.0s"
				em.Continuous = true;
				em.Count = ParseRange(tokens.Length > 0 ? tokens[0] : "1");
				var perIdx = Array.FindIndex(tokens, t => t.Equals("per", StringComparison.OrdinalIgnoreCase));
				if (perIdx >= 0 && perIdx + 1 < tokens.Length) {
					em.RateInterval = ParseRange(tokens[perIdx + 1]);
				}

				break;
			case "count":
				em.Continuous = false;
				em.Count = ParseRange(tokens.Length > 0 ? tokens[0] : "1");
				break;
			case "origin":
				ParseOrigin(em, tokens, errors, lineNo);
				break;
			case "vel":
			case "velocity":
				ParseVelocity(em, tokens, errors, lineNo);
				break;
			case "gravity":
				ParseGravity(em, tokens);
				break;
			case "color":
				ParseColorSpec(em, value);
				break;
			case "colorshift":
				em.HueShift = ParseSignedNumber(value);
				break;
			case "fade":
				em.Life = ParseRange(value);
				break;
			case "life":
				em.Life = ParseRange(value);
				break;
			case "die":
				em.DieOnApex = value.Contains("vy", StringComparison.OrdinalIgnoreCase) ||
				               value.Contains("apex", StringComparison.OrdinalIgnoreCase);
				break;
			case "spawn":
				if (tokens.Length > 0) {
					em.SpawnOnDeath = tokens[0];
					var chanceTok = tokens.FirstOrDefault(t => t.StartsWith('@') || t.Contains('.'));
					if (chanceTok != null) {
						em.SpawnChance = Math.Clamp(ParseDouble(chanceTok.TrimStart('@')), 0, 1);
					}
				}

				break;
			default:
				errors.Add($"Line {lineNo}: unknown property '{key}'.");
				break;
		}
	}

	private static void ParseOrigin(EmitterSpec em, IEnumerable<string> tokens, List<string> errors, int lineNo) {
		var origin = new OriginSpec();
		foreach (var tok in tokens) {
			if (tok.Equals("parent", StringComparison.OrdinalIgnoreCase)) {
				origin.FromParent = true;
				continue;
			}

			var kv = tok.Split(':', 2);
			if (kv.Length != 2) {
				errors.Add($"Line {lineNo}: origin token '{tok}' should be x:.. or y:..");
				continue;
			}

			var axis = kv[0].ToLowerInvariant();
			var v = kv[1].ToLowerInvariant();
			var (mode, pos) = ParseAxis(v);
			if (axis == "x") {
				origin.XMode = mode;
				origin.X = pos;
			} else if (axis == "y") {
				origin.YMode = mode;
				origin.Y = pos;
			}
		}

		em.Origin = origin;
	}

	private static (OriginAxis, double) ParseAxis(string v) {
		return v switch {
			"rand" or "random" => (OriginAxis.Random, 0.5),
			"left" or "top" => (OriginAxis.Fixed, 0.0),
			"right" or "bottom" => (OriginAxis.Fixed, 1.0),
			"center" or "middle" => (OriginAxis.Fixed, 0.5),
			_ => (OriginAxis.Fixed, Math.Clamp(ParseDouble(v), 0, 1))
		};
	}

	private static void ParseVelocity(EmitterSpec em, IEnumerable<string> tokens, List<string> errors, int lineNo) {
		foreach (var tok in tokens) {
			var kv = tok.Split(':', 2);
			if (kv.Length != 2) {
				errors.Add($"Line {lineNo}: vel token '{tok}' should be dir:.. or speed:..");
				continue;
			}

			var k = kv[0].ToLowerInvariant();
			var v = kv[1];
			switch (k) {
				case "dir":
					em.Direction = v.ToLowerInvariant() switch {
						"up" => SpawnDirection.Up,
						"down" => SpawnDirection.Down,
						"left" => SpawnDirection.Left,
						"right" => SpawnDirection.Right,
						"radial" or "out" => SpawnDirection.Radial,
						"none" or "still" => SpawnDirection.None,
						_ => em.Direction
					};
					break;
				case "speed":
					em.Speed = ParseRange(v);
					break;
			}
		}
	}

	private static void ParseGravity(EmitterSpec em, IReadOnlyList<string> tokens) {
		if (tokens.Count == 0) {
			return;
		}

		var dir = tokens[0].ToLowerInvariant();
		var strength = tokens.Count > 1 ? ParseDouble(tokens[1]) : 0;
		switch (dir) {
			case "down":
				em.GravityY = strength;
				break;
			case "up":
				em.GravityY = -strength;
				break;
			case "left":
				em.GravityX = -strength;
				break;
			case "right":
				em.GravityX = strength;
				break;
			case "none":
				em.GravityX = 0;
				em.GravityY = 0;
				break;
		}
	}

	private static void ParseColorSpec(EmitterSpec em, string value) {
		var v = value.Trim();
		if (v.StartsWith("pick(", StringComparison.OrdinalIgnoreCase) && v.EndsWith(')')) {
			em.ColorMode = ColorMode.Pick;
			var inner = v[5..^1];
			foreach (var c in inner.Split(',', StringSplitOptions.RemoveEmptyEntries)) {
				em.Palette.Add(ParseColor(c.Trim()));
			}

			return;
		}

		if (v.Equals("parent", StringComparison.OrdinalIgnoreCase)) {
			em.ColorMode = ColorMode.Parent;
		} else if (v.Equals("random", StringComparison.OrdinalIgnoreCase)) {
			em.ColorMode = ColorMode.Random;
		} else if (v.Equals("rainbow", StringComparison.OrdinalIgnoreCase)) {
			em.ColorMode = ColorMode.Rainbow;
		} else {
			em.ColorMode = ColorMode.Fixed;
			em.Color = ParseColor(v);
		}
	}

	private static NumRange ParseRange(string token) {
		var t = token.Trim().TrimEnd('s');
		var parts = t.Split("..", StringSplitOptions.RemoveEmptyEntries);
		if (parts.Length == 2) {
			return new NumRange(ParseDouble(parts[0]), ParseDouble(parts[1]));
		}

		return new NumRange(ParseDouble(t));
	}

	private static double ParseDouble(string s) {
		s = s.Trim();
		return double.TryParse(s, NumberStyles.Any, CultureInfo.InvariantCulture, out var v) ? v : 0;
	}

	private static double ParseSignedNumber(string s) {
		var filtered = new string(s.Where(c => char.IsDigit(c) || c is '-' or '+' or '.').ToArray());
		filtered = filtered.Replace("+", "");
		return ParseDouble(filtered);
	}

	private static Color ParseColor(string hex) {
		try {
			return ColorTranslator.FromHtml(hex.Trim());
		} catch (Exception) {
			throw new FormatException($"invalid color '{hex}'.");
		}
	}
}
