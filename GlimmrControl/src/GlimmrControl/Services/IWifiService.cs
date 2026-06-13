using GlimmrControl.Models;

namespace GlimmrControl.Services;

/// <summary>
///     Platform abstraction for the WiFi-onboarding flow: finding the Glimmr setup hotspot
///     (<c>glimmr-*</c>), joining it, and reporting the current SSID. Implementations live under
///     <c>Platforms/{Android,iOS,Windows}</c>.
/// </summary>
public interface IWifiService
{
	/// <summary>
	///     True if the platform can enumerate nearby SSIDs. iOS returns false (Apple forbids SSID
	///     scanning without a special entitlement), so the UI offers a manual "join" flow there.
	/// </summary>
	bool CanScan { get; }

	/// <summary>The SSID prefix the Glimmr setup hotspot uses (comitup <c>ap_name: glimmr-&lt;nnn&gt;</c>).</summary>
	string SetupSsidPrefix => "glimmr-";

	/// <summary>Request the runtime permissions needed for scanning/joining. Returns true if granted.</summary>
	Task<bool> EnsurePermissionsAsync();

	/// <summary>Enumerate nearby Glimmr setup hotspots. Empty when <see cref="CanScan" /> is false.</summary>
	Task<IReadOnlyList<WifiNetwork>> ScanSetupNetworksAsync(CancellationToken ct = default);

	/// <summary>The SSID the device is currently joined to, or null if unknown / not on WiFi.</summary>
	Task<string?> GetCurrentSsidAsync();

	/// <summary>
	///     Join the (open) Glimmr setup hotspot and route app traffic over it so HTTP requests to
	///     the comitup portal at 10.41.0.1 succeed. Returns true on success.
	/// </summary>
	Task<bool> JoinSetupNetworkAsync(string ssid, CancellationToken ct = default);

	/// <summary>Release any binding/connection created by <see cref="JoinSetupNetworkAsync" />.</summary>
	Task DisconnectSetupNetworkAsync();
}

/// <summary>No-op fallback used on platforms without a concrete implementation.</summary>
public sealed class NullWifiService : IWifiService
{
	public bool CanScan => false;
	public Task<bool> EnsurePermissionsAsync() => Task.FromResult(false);

	public Task<IReadOnlyList<WifiNetwork>> ScanSetupNetworksAsync(CancellationToken ct = default) =>
		Task.FromResult<IReadOnlyList<WifiNetwork>>(Array.Empty<WifiNetwork>());

	public Task<string?> GetCurrentSsidAsync() => Task.FromResult<string?>(null);
	public Task<bool> JoinSetupNetworkAsync(string ssid, CancellationToken ct = default) => Task.FromResult(false);
	public Task DisconnectSetupNetworkAsync() => Task.CompletedTask;
}
