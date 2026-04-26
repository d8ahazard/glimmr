#region

using System.Threading;
using System.Threading.Tasks;
using Glimmr.Services;
using Serilog;

#endregion

namespace Glimmr.Models.ColorTarget.Tuya;

public class TuyaDiscovery : ColorDiscovery, IColorDiscovery {
	public TuyaDiscovery(ColorService cs) : base(cs) {
	}

	/// <summary>
	///     Tuya devices require cloud authentication for auto-discovery.
	///     Devices must be added manually via the UI with their device ID,
	///     IP address, and local key from the Tuya developer portal.
	/// </summary>
	public Task Discover(int timeout, CancellationToken ct) {
		Log.Debug("Tuya: Discovery is manual-only (cloud auth required for auto-discovery).");
		return Task.CompletedTask;
	}
}
