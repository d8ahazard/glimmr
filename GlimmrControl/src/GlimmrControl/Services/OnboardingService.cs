using GlimmrControl.Models;

namespace GlimmrControl.Services;

/// <summary>
///     Orchestrates the WiFi onboarding wizard: find the Glimmr setup hotspot, join it, hand the
///     home WiFi credentials to comitup, then rediscover the device once it's on the home network.
///     The page/ViewModel drives these steps and handles the user prompts in between.
/// </summary>
public sealed class OnboardingService
{
	private readonly ComitupClient _comitup;
	private readonly DiscoveryService _discovery;
	private readonly IWifiService _wifi;

	public OnboardingService(IWifiService wifi, DiscoveryService discovery)
	{
		_wifi = wifi;
		_discovery = discovery;
		_comitup = new ComitupClient();
	}

	/// <summary>Whether the platform can list nearby hotspots (false on iOS).</summary>
	public bool CanScanForHotspots => _wifi.CanScan;

	public Task<bool> EnsurePermissionsAsync() => _wifi.EnsurePermissionsAsync();

	public Task<IReadOnlyList<WifiNetwork>> FindSetupHotspotsAsync(CancellationToken ct = default) =>
		_wifi.CanScan ? _wifi.ScanSetupNetworksAsync(ct) : Task.FromResult<IReadOnlyList<WifiNetwork>>(Array.Empty<WifiNetwork>());

	public Task<string?> GetCurrentSsidAsync() => _wifi.GetCurrentSsidAsync();

	/// <summary>Join the setup hotspot and confirm the comitup portal answers.</summary>
	public async Task<bool> JoinSetupHotspotAsync(string ssid, CancellationToken ct = default)
	{
		var joined = await _wifi.JoinSetupNetworkAsync(ssid, ct).ConfigureAwait(false);
		if (!joined)
		{
			return false;
		}

		return await WaitForPortalAsync(TimeSpan.FromSeconds(15), ct).ConfigureAwait(false);
	}

	/// <summary>True once the comitup captive portal is reachable (phone is on the setup AP).</summary>
	public async Task<bool> WaitForPortalAsync(TimeSpan timeout, CancellationToken ct = default)
	{
		var deadline = DateTime.UtcNow + timeout;
		while (DateTime.UtcNow < deadline)
		{
			ct.ThrowIfCancellationRequested();
			if (await _comitup.IsPortalReachableAsync(ct).ConfigureAwait(false))
			{
				return true;
			}

			await Task.Delay(1500, ct).ConfigureAwait(false);
		}

		return false;
	}

	public Task<IReadOnlyList<WifiNetwork>> ListHomeNetworksAsync(CancellationToken ct = default) =>
		_comitup.ListNetworksAsync(ct);

	/// <summary>
	///     Hand the chosen home network credentials to comitup. The Pi will drop the hotspot and try
	///     to join; the phone is released from the setup AP so it can return to the home network.
	/// </summary>
	public async Task<bool> SubmitHomeCredentialsAsync(string ssid, string password, CancellationToken ct = default)
	{
		var ok = await _comitup.SubmitCredentialsAsync(ssid, password, ct).ConfigureAwait(false);
		await _wifi.DisconnectSetupNetworkAsync().ConfigureAwait(false);
		return ok;
	}

	/// <summary>
	///     Poll mDNS for the freshly-provisioned Glimmr after the user rejoins their home WiFi.
	///     Returns the first discovered device, or null on timeout.
	/// </summary>
	public async Task<GlimmrDevice?> WaitForProvisionedDeviceAsync(TimeSpan timeout, CancellationToken ct = default)
	{
		var deadline = DateTime.UtcNow + timeout;
		while (DateTime.UtcNow < deadline)
		{
			ct.ThrowIfCancellationRequested();
			var found = await _discovery.ScanAsync(TimeSpan.FromSeconds(5), ct).ConfigureAwait(false);
			if (found.Count > 0)
			{
				return found[0];
			}

			await Task.Delay(2000, ct).ConfigureAwait(false);
		}

		return null;
	}
}
