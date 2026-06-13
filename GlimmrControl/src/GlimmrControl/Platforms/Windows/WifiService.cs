using GlimmrControl.Models;
using GlimmrControl.Services;
using Windows.Devices.WiFi;
using Windows.Networking.Connectivity;

namespace GlimmrControl.Platforms.Windows;

/// <summary>
///     Windows WiFi onboarding via the WinRT <see cref="WiFiAdapter" />. Scans for <c>glimmr-*</c>
///     hotspots and joins the open setup AP so the comitup portal (10.41.0.1) becomes reachable.
///     Note: unpackaged desktop apps may have WiFi control denied by the OS; the UI degrades to the
///     embedded portal flow when that happens.
/// </summary>
public sealed class WifiService : IWifiService
{
	private WiFiAdapter? _adapter;

	public bool CanScan => true;

	public async Task<bool> EnsurePermissionsAsync()
	{
		try
		{
			var access = await WiFiAdapter.RequestAccessAsync();
			if (access != WiFiAccessStatus.Allowed)
			{
				return false;
			}

			_adapter ??= await GetAdapterAsync();
			return _adapter != null;
		}
		catch
		{
			return false;
		}
	}

	public async Task<IReadOnlyList<WifiNetwork>> ScanSetupNetworksAsync(CancellationToken ct = default)
	{
		var results = new List<WifiNetwork>();
		try
		{
			_adapter ??= await GetAdapterAsync();
			if (_adapter == null)
			{
				return results;
			}

			await _adapter.ScanAsync();
			var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
			foreach (var net in _adapter.NetworkReport.AvailableNetworks)
			{
				var ssid = net.Ssid;
				if (string.IsNullOrEmpty(ssid) ||
					!ssid.StartsWith("glimmr-", StringComparison.OrdinalIgnoreCase) ||
					!seen.Add(ssid))
				{
					continue;
				}

				results.Add(new WifiNetwork
				{
					Ssid = ssid,
					IsGlimmrSetup = true,
					Strength = (int)Math.Round(net.SignalBars / 4.0 * 100),
					Secured = net.SecuritySettings.NetworkAuthenticationType != NetworkAuthenticationType.Open80211
				});
			}
		}
		catch
		{
			// scan failed / denied
		}

		return results;
	}

	public Task<string?> GetCurrentSsidAsync()
	{
		try
		{
			var profile = NetworkInformation.GetInternetConnectionProfile();
			var ssid = profile?.WlanConnectionProfileDetails?.GetConnectedSsid();
			return Task.FromResult(string.IsNullOrEmpty(ssid) ? null : ssid);
		}
		catch
		{
			return Task.FromResult<string?>(null);
		}
	}

	public async Task<bool> JoinSetupNetworkAsync(string ssid, CancellationToken ct = default)
	{
		try
		{
			_adapter ??= await GetAdapterAsync();
			if (_adapter == null)
			{
				return false;
			}

			await _adapter.ScanAsync();
			var target = _adapter.NetworkReport.AvailableNetworks
				.FirstOrDefault(n => string.Equals(n.Ssid, ssid, StringComparison.OrdinalIgnoreCase));
			if (target == null)
			{
				return false;
			}

			// Glimmr setup hotspot is open - no credential needed.
			var result = await _adapter.ConnectAsync(target, WiFiReconnectionKind.Manual);
			return result.ConnectionStatus == WiFiConnectionStatus.Success;
		}
		catch
		{
			return false;
		}
	}

	public Task DisconnectSetupNetworkAsync()
	{
		try
		{
			_adapter?.Disconnect();
		}
		catch
		{
			// ignore
		}

		return Task.CompletedTask;
	}

	private static async Task<WiFiAdapter?> GetAdapterAsync()
	{
		var adapters = await WiFiAdapter.FindAllAdaptersAsync();
		return adapters.FirstOrDefault();
	}
}
