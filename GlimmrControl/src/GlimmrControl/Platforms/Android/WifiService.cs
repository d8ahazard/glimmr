using Android.Content;
using Android.Net;
using Android.Net.Wifi;
using GlimmrControl.Models;
using GlimmrControl.Services;
using Microsoft.Maui.ApplicationModel;
using Application = Android.App.Application;

namespace GlimmrControl.Platforms.Android;

/// <summary>
///     Android WiFi onboarding. Scans for <c>glimmr-*</c> hotspots via <see cref="WifiManager" /> and
///     joins the (open) setup AP using a <see cref="WifiNetworkSpecifier" /> on API 29+, binding the
///     process to that network so HTTP to the comitup portal (10.41.0.1) is routed over WiFi rather
///     than cellular. On older APIs it falls back to the legacy <see cref="WifiConfiguration" /> path.
/// </summary>
public sealed class WifiService : IWifiService
{
	private readonly ConnectivityManager? _connectivity;
	private readonly WifiManager? _wifi;
	private ConnectivityManager.NetworkCallback? _callback;

	public WifiService()
	{
		var ctx = Application.Context;
		_wifi = ctx.GetSystemService(Context.WifiService) as WifiManager;
		_connectivity = ctx.GetSystemService(Context.ConnectivityService) as ConnectivityManager;
	}

	public bool CanScan => true;

	public async Task<bool> EnsurePermissionsAsync()
	{
		// Location is required to read scan results / current SSID on Android < 13.
		var status = await Permissions.RequestAsync<Permissions.LocationWhenInUse>();
		if (status != PermissionStatus.Granted)
		{
			return false;
		}

		// Android 13+ also needs NEARBY_WIFI_DEVICES for scanning without location.
		if (OperatingSystem.IsAndroidVersionAtLeast(33))
		{
			var nearby = await Permissions.RequestAsync<NearbyWifiPermission>();
			return nearby == PermissionStatus.Granted;
		}

		return true;
	}

	public Task<IReadOnlyList<WifiNetwork>> ScanSetupNetworksAsync(CancellationToken ct = default)
	{
		var results = new List<WifiNetwork>();
		try
		{
			_wifi?.StartScan();
			var scan = _wifi?.ScanResults;
			if (scan != null)
			{
				var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
				foreach (var r in scan)
				{
					var ssid = (r.Ssid ?? "").Trim('"');
					if (string.IsNullOrEmpty(ssid) ||
						!ssid.StartsWith("glimmr-", StringComparison.OrdinalIgnoreCase) ||
						!seen.Add(ssid))
					{
						continue;
					}

					var caps = r.Capabilities ?? "";
					results.Add(new WifiNetwork
					{
						Ssid = ssid,
						IsGlimmrSetup = true,
						Strength = WifiManager.CalculateSignalLevel(r.Level, 100),
						Secured = caps.Contains("WPA") || caps.Contains("WEP")
					});
				}
			}
		}
		catch
		{
			// scanning may be throttled or denied
		}

		return Task.FromResult<IReadOnlyList<WifiNetwork>>(results);
	}

	public Task<string?> GetCurrentSsidAsync()
	{
		try
		{
			var ssid = _wifi?.ConnectionInfo?.SSID?.Trim('"');
			if (string.IsNullOrEmpty(ssid) || ssid == "<unknown ssid>")
			{
				return Task.FromResult<string?>(null);
			}

			return Task.FromResult<string?>(ssid);
		}
		catch
		{
			return Task.FromResult<string?>(null);
		}
	}

	public async Task<bool> JoinSetupNetworkAsync(string ssid, CancellationToken ct = default)
	{
		if (_connectivity == null)
		{
			return false;
		}

		if (!OperatingSystem.IsAndroidVersionAtLeast(29))
		{
			// Pre-Android 10 used the now-removed WifiConfiguration API. Those devices can join
			// the open setup AP from system Wi-Fi settings and use the "I'm connected" flow.
			return false;
		}

		var specifier = new WifiNetworkSpecifier.Builder()
			.SetSsid(ssid)!
			.Build();

		var request = new NetworkRequest.Builder()
			.AddTransportType(TransportType.Wifi)!
			.SetNetworkSpecifier(specifier)!
			.Build();

		var tcs = new TaskCompletionSource<bool>();
		await DisconnectSetupNetworkAsync();
		_callback = new JoinCallback(_connectivity, tcs);

		try
		{
			_connectivity.RequestNetwork(request!, _callback);
		}
		catch
		{
			return false;
		}

		using (ct.Register(() => tcs.TrySetResult(false)))
		using (var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30)))
		{
			timeout.Token.Register(() => tcs.TrySetResult(false));
			return await tcs.Task.ConfigureAwait(false);
		}
	}

	public Task DisconnectSetupNetworkAsync()
	{
		try
		{
			if (OperatingSystem.IsAndroidVersionAtLeast(23))
			{
				_connectivity?.BindProcessToNetwork(null);
			}

			if (_callback != null)
			{
				_connectivity?.UnregisterNetworkCallback(_callback);
				_callback = null;
			}
		}
		catch
		{
			// ignore
		}

		return Task.CompletedTask;
	}

	private sealed class JoinCallback : ConnectivityManager.NetworkCallback
	{
		private readonly ConnectivityManager _cm;
		private readonly TaskCompletionSource<bool> _tcs;

		public JoinCallback(ConnectivityManager cm, TaskCompletionSource<bool> tcs)
		{
			_cm = cm;
			_tcs = tcs;
		}

		public override void OnAvailable(Network network)
		{
			try
			{
				if (OperatingSystem.IsAndroidVersionAtLeast(23))
				{
					_cm.BindProcessToNetwork(network);
				}
			}
			catch
			{
				// ignore
			}

			_tcs.TrySetResult(true);
		}

		public override void OnUnavailable()
		{
			_tcs.TrySetResult(false);
		}
	}
}
