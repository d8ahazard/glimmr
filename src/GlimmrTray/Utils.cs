#region

using System;
using System.Collections.Generic;

#endregion

namespace GlimmrTray; 

internal sealed class CmdArgumentException : Exception {
	public CmdArgumentException(string message) : base(message) {
	}
}

internal static class Utils {
	public static T GetOrDefault<K, T>(this IDictionary<K, T> dict, K key, T t = default(T)) {
		if (dict.TryGetValue(key, out var v)) {
			return v;
		}

		return t;
	}

	public static Dictionary<string, string[]> GetCommondLines(string[] args) {
		var cmds = new Dictionary<string, string[]>();

		var key = "";
		var values = new List<string>();

		foreach (var i in args) {
			if (i.StartsWith("-")) {
				if (!string.IsNullOrEmpty(key)) {
					cmds.Add(key, values.ToArray());
					key = "";
					values.Clear();
				}

				key = i;
			} else {
				values.Add(i);
			}
		}

		if (!string.IsNullOrEmpty(key)) {
			cmds.Add(key, values.ToArray());
		}

		return cmds;
	}

	public static string GetArgument(this Dictionary<string, string[]> args, string name, bool isOption = false) {
		var values = args.GetOrDefault(name);
		if (values == null || values.Length == 0) {
			if (isOption) {
				return null;
			}

			throw new CmdArgumentException(name + " is not found");
		}

		return values[0];
	}
}