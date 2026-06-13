using GlimmrControl.Models;
using Zeroconf;

namespace GlimmrControl.Services;

/// <summary>
///     Discovers Glimmr servers on the local network via mDNS/Bonjour (the <c>_glimmr._tcp</c>
///     service comitup/avahi advertises). Each hit is verified by probing its REST API.
/// </summary>
public sealed class DiscoveryService
{
	private const string ServiceType = "_glimmr._tcp.local.";

	/// <summary>
	///     Browse for Glimmr devices. Returns verified <see cref="GlimmrDevice" />s with their
	///     resolved host/IP and reported name/mode.
	/// </summary>
	public async Task<IReadOnlyList<GlimmrDevice>> ScanAsync(TimeSpan? timeout = null,
		CancellationToken ct = default)
	{
		var results = new List<GlimmrDevice>();
		try
		{
			var scan = timeout ?? TimeSpan.FromSeconds(5);
			var hosts = await ZeroconfResolver.ResolveAsync(ServiceType, scan).ConfigureAwait(false);
			foreach (var host in hosts)
			{
				ct.ThrowIfCancellationRequested();
				var address = host.IPAddress;
				if (string.IsNullOrEmpty(address))
				{
					continue;
				}

				var device = new GlimmrDevice(address, host.DisplayName ?? address);

				// Verify it's really a Glimmr by probing the API; fill in the friendly name.
				using var api = new GlimmrApiClient(device.BaseUrl);
				var sd = await api.GetSystemDataAsync(ct).ConfigureAwait(false);
				if (sd == null)
				{
					continue;
				}

				device.Reachability = DeviceReachability.Online;
				device.DeviceMode = sd.DeviceMode;
				device.DeviceId = string.IsNullOrEmpty(sd.DeviceId) ? device.DeviceId : sd.DeviceId;
				if (!string.IsNullOrEmpty(sd.DeviceName))
				{
					device.Name = sd.DeviceName;
				}

				results.Add(device);
			}
		}
		catch (OperationCanceledException)
		{
			// caller cancelled
		}
		catch
		{
			// mDNS can fail on networks/permissions; treat as "nothing found"
		}

		return results;
	}
}
