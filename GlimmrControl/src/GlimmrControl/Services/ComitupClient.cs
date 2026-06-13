using System.Text.RegularExpressions;
using GlimmrControl.Models;

namespace GlimmrControl.Services;

/// <summary>
///     Talks to the comitup captive portal that a freshly-flashed Glimmr serves on its
///     <c>glimmr-*</c> hotspot (http://10.41.0.1). Comitup exposes no JSON API, so the network
///     list is scraped from the rendered <c>index.html</c> and credentials are submitted to the
///     same <c>POST /connect</c> form the portal itself uses.
/// </summary>
public sealed partial class ComitupClient
{
	public const string PortalUrl = "http://10.41.0.1";

	[GeneratedRegex(@"confirm\?ssid=(?<ssid>[^&""]+)&encrypted=(?<enc>encrypted|unencrypted)",
		RegexOptions.IgnoreCase)]
	private static partial Regex PointRegex();

	private readonly HttpClient _http;

	public ComitupClient(HttpClient? http = null)
	{
		_http = http ?? new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
	}

	/// <summary>Fetch the list of home networks the Pi can currently see.</summary>
	public async Task<IReadOnlyList<WifiNetwork>> ListNetworksAsync(CancellationToken ct = default)
	{
		var list = new List<WifiNetwork>();
		try
		{
			var html = await _http.GetStringAsync(PortalUrl + "/", ct).ConfigureAwait(false);
			var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
			foreach (Match m in PointRegex().Matches(html))
			{
				var encoded = m.Groups["ssid"].Value;
				var ssid = Uri.UnescapeDataString(encoded);
				if (string.IsNullOrWhiteSpace(ssid) || !seen.Add(ssid))
				{
					continue;
				}

				list.Add(new WifiNetwork
				{
					Ssid = ssid,
					Secured = m.Groups["enc"].Value.Equals("encrypted", StringComparison.OrdinalIgnoreCase)
				});
			}
		}
		catch
		{
			// portal unreachable - caller surfaces an error
		}

		return list;
	}

	/// <summary>
	///     Submit home WiFi credentials. After this returns, comitup tears down the hotspot and the
	///     Pi attempts to join the chosen network (the phone will drop off the setup AP).
	/// </summary>
	public async Task<bool> SubmitCredentialsAsync(string ssid, string password, CancellationToken ct = default)
	{
		try
		{
			// The portal's confirm form posts the url-quoted SSID; reproduce that so SSIDs with
			// spaces/special characters round-trip through comitup's unquote() correctly.
			var form = new FormUrlEncodedContent(new[]
			{
				new KeyValuePair<string, string>("ssid", Uri.EscapeDataString(ssid)),
				new KeyValuePair<string, string>("password", password)
			});
			var res = await _http.PostAsync(PortalUrl + "/connect", form, ct).ConfigureAwait(false);
			return res.IsSuccessStatusCode;
		}
		catch
		{
			return false;
		}
	}

	/// <summary>True if the comitup portal answers - i.e. the phone is on a Glimmr setup hotspot.</summary>
	public async Task<bool> IsPortalReachableAsync(CancellationToken ct = default)
	{
		try
		{
			using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
			cts.CancelAfter(TimeSpan.FromSeconds(4));
			var res = await _http.GetAsync(PortalUrl + "/", cts.Token).ConfigureAwait(false);
			return res.IsSuccessStatusCode;
		}
		catch
		{
			return false;
		}
	}
}
