using GlimmrControl.Models;
using GlimmrControl.Services;
using NetworkExtension;

namespace GlimmrControl.Platforms.iOS;

/// <summary>
///     iOS WiFi onboarding. Apple forbids enumerating nearby SSIDs without a special
///     <c>NEHotspotHelper</c> entitlement, so <see cref="CanScan" /> is false and the UI guides the
///     user to type/join the <c>glimmr-*</c> hotspot. Joining uses <see cref="NEHotspotConfiguration" />,
///     and the current SSID is read via <see cref="NEHotspotNetwork" /> (requires the
///     "Access WiFi Information" entitlement).
/// </summary>
public sealed class WifiService : IWifiService
{
	private string? _joinedSsid;

	public bool CanScan => false;

	public Task<bool> EnsurePermissionsAsync() => Task.FromResult(true);

	public Task<IReadOnlyList<WifiNetwork>> ScanSetupNetworksAsync(CancellationToken ct = default) =>
		Task.FromResult<IReadOnlyList<WifiNetwork>>(Array.Empty<WifiNetwork>());

	public Task<string?> GetCurrentSsidAsync()
	{
		var tcs = new TaskCompletionSource<string?>();
		try
		{
			NEHotspotNetwork.FetchCurrent(network => tcs.TrySetResult(network?.Ssid));
		}
		catch
		{
			tcs.TrySetResult(null);
		}

		return tcs.Task;
	}

	public async Task<bool> JoinSetupNetworkAsync(string ssid, CancellationToken ct = default)
	{
		try
		{
			// The Glimmr setup hotspot is open (comitup ships with no ap_password).
			var config = new NEHotspotConfiguration(ssid) { JoinOnce = true };
			var tcs = new TaskCompletionSource<bool>();
			NEHotspotConfigurationManager.SharedManager.ApplyConfiguration(config, error =>
			{
				// A nil error - or the "already associated" error - both mean we're connected.
				var ok = error == null ||
						 error.LocalizedDescription.Contains("already associated", StringComparison.OrdinalIgnoreCase);
				tcs.TrySetResult(ok);
			});

			using (ct.Register(() => tcs.TrySetResult(false)))
			{
				var applied = await tcs.Task.ConfigureAwait(false);
				if (!applied)
				{
					return false;
				}
			}

			_joinedSsid = ssid;

			// Give iOS a moment to associate before the portal is probed.
			await Task.Delay(2500, ct).ConfigureAwait(false);
			var current = await GetCurrentSsidAsync().ConfigureAwait(false);
			return current == null || current.Equals(ssid, StringComparison.OrdinalIgnoreCase);
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
			// JoinOnce=true configurations are removed automatically; remove explicitly as well.
			if (!string.IsNullOrEmpty(_joinedSsid))
			{
				NEHotspotConfigurationManager.SharedManager.RemoveConfiguration(_joinedSsid);
				_joinedSsid = null;
			}
		}
		catch
		{
			// ignore
		}

		return Task.CompletedTask;
	}
}
