using System.Text.Json;

namespace HueSyncTestClient;

/// <summary>
///     Helpers for parsing the emulated bridge's JSON responses.
/// </summary>
public static class HueRegistration {
	/// <summary>
	///     Parse the v1 registration response: [{"success":{"username":"..","clientkey":".."}}].
	/// </summary>
	public static (string username, string clientKey)? Parse(string json) {
		try {
			using var doc = JsonDocument.Parse(json);
			if (doc.RootElement.ValueKind != JsonValueKind.Array) {
				return null;
			}

			foreach (var entry in doc.RootElement.EnumerateArray()) {
				if (!entry.TryGetProperty("success", out var success)) {
					continue;
				}

				var user = success.TryGetProperty("username", out var u) ? u.GetString() : null;
				var key = success.TryGetProperty("clientkey", out var k) ? k.GetString() : null;
				if (!string.IsNullOrEmpty(user) && !string.IsNullOrEmpty(key)) {
					return (user!, key!);
				}
			}
		} catch {
			// fall through
		}

		return null;
	}

	/// <summary>
	///     Count channels in the first v2 entertainment_configuration.
	/// </summary>
	public static int? CountChannels(string json) {
		try {
			using var doc = JsonDocument.Parse(json);
			var data = doc.RootElement.GetProperty("data");
			foreach (var cfg in data.EnumerateArray()) {
				if (cfg.TryGetProperty("channels", out var channels)) {
					return channels.GetArrayLength();
				}
			}
		} catch {
			// fall through
		}

		return null;
	}

	/// <summary>
	///     Return the id of the first v2 entertainment_configuration.
	/// </summary>
	public static string? FirstConfigId(string json) {
		try {
			using var doc = JsonDocument.Parse(json);
			var data = doc.RootElement.GetProperty("data");
			foreach (var cfg in data.EnumerateArray()) {
				if (cfg.TryGetProperty("id", out var id)) {
					return id.GetString();
				}
			}
		} catch {
			// fall through
		}

		return null;
	}
}
