#region

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using Glimmr.Models.Data;

#endregion

namespace Glimmr.Models.ColorSource.Scene.Script;

/// <summary>
///     Resolves a scene's tunable parameters (slider values) and substitutes them into a script.
///     Defaults come from each <see cref="SceneParam" />; per-scene user overrides live in
///     <see cref="SystemData.SceneParams" /> keyed by scene id. Script scenes reference values as
///     <c>${Key}</c> tokens; built-in providers receive the resolved dictionary directly.
/// </summary>
public static class SceneParams {
	/// <summary>
	///     Build the effective value map for a scene: each declared param's default, overridden by
	///     any stored value, clamped to the param's [Min, Max] range.
	/// </summary>
	public static Dictionary<string, double> Resolve(SceneV2 scene, SystemData? sd) {
		var values = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
		if (scene.Params.Count == 0) {
			return values;
		}

		Dictionary<string, double>? overrides = null;
		if (sd != null && !string.IsNullOrEmpty(scene.Id) &&
		    sd.SceneParams.TryGetValue(scene.Id, out var stored)) {
			overrides = stored;
		}

		foreach (var p in scene.Params) {
			if (string.IsNullOrEmpty(p.Key)) {
				continue;
			}

			var v = p.Default;
			if (overrides != null && overrides.TryGetValue(p.Key, out var ov)) {
				v = ov;
			}

			values[p.Key] = Math.Clamp(v, Math.Min(p.Min, p.Max), Math.Max(p.Min, p.Max));
		}

		return values;
	}

	/// <summary>
	///     Replace <c>${key}</c> tokens in a script with their resolved numeric values. Unknown
	///     tokens are left untouched (the parser tolerates them as zeros). Values are formatted
	///     invariantly so a comma locale can't corrupt the script.
	/// </summary>
	public static string Apply(string script, IReadOnlyDictionary<string, double> values) {
		if (string.IsNullOrEmpty(script) || script.IndexOf("${", StringComparison.Ordinal) < 0) {
			return script ?? string.Empty;
		}

		var sb = new StringBuilder(script.Length);
		for (var i = 0; i < script.Length;) {
			if (script[i] == '$' && i + 1 < script.Length && script[i + 1] == '{') {
				var end = script.IndexOf('}', i + 2);
				if (end > i + 1) {
					var key = script.Substring(i + 2, end - (i + 2)).Trim();
					if (values.TryGetValue(key, out var v)) {
						sb.Append(Format(v));
						i = end + 1;
						continue;
					}
				}
			}

			sb.Append(script[i]);
			i++;
		}

		return sb.ToString();
	}

	private static string Format(double v) {
		// Whole numbers print without a trailing ".0" so counts read naturally.
		return v == Math.Floor(v) && !double.IsInfinity(v)
			? ((long)v).ToString(CultureInfo.InvariantCulture)
			: v.ToString("0.###", CultureInfo.InvariantCulture);
	}
}
