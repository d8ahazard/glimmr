using System.Text;
using System.Text.Json;
using GlimmrControl.Models;

namespace GlimmrControl.Services;

/// <summary>
///     REST client for a single Glimmr server (<c>/api/Glimmr</c>). Used for one-shot actions,
///     reachability probes, and as a fallback when the SignalR hub is unavailable.
/// </summary>
public sealed class GlimmrApiClient : IDisposable
{
	private readonly HttpClient _http;
	private readonly bool _ownsHttp;

	public GlimmrApiClient(string baseUrl, HttpClient? http = null)
	{
		BaseUrl = baseUrl.TrimEnd('/');
		if (http != null)
		{
			_http = http;
		}
		else
		{
			_http = new HttpClient { Timeout = TimeSpan.FromSeconds(6) };
			_ownsHttp = true;
		}
	}

	public string BaseUrl { get; }

	private string Api(string path) => $"{BaseUrl}/api/Glimmr{path}";

	/// <summary>Probe the device; returns its SystemData or null if unreachable / not a Glimmr.</summary>
	public async Task<SystemData?> GetSystemDataAsync(CancellationToken ct = default)
	{
		try
		{
			var json = await _http.GetStringAsync(Api(""), ct).ConfigureAwait(false);
			return JsonSerializer.Deserialize<SystemData>(json, GlimmrJson.Options);
		}
		catch
		{
			return null;
		}
	}

	/// <summary>Full datastore (system, devices, scenes, stats) - used when the hub is unavailable.</summary>
	public async Task<StoreData?> GetStoreAsync(CancellationToken ct = default)
	{
		try
		{
			var json = await _http.GetStringAsync(Api("/store"), ct).ConfigureAwait(false);
			return JsonSerializer.Deserialize<StoreData>(json, GlimmrJson.Options);
		}
		catch
		{
			return null;
		}
	}

	public async Task<List<DeviceData>> GetDevicesAsync(CancellationToken ct = default)
	{
		try
		{
			var json = await _http.GetStringAsync(Api("/devices"), ct).ConfigureAwait(false);
			return JsonSerializer.Deserialize<List<DeviceData>>(json, GlimmrJson.Options) ?? new List<DeviceData>();
		}
		catch
		{
			return new List<DeviceData>();
		}
	}

	public Task<bool> SetModeAsync(int mode, CancellationToken ct = default) =>
		PostJsonAsync("/mode", mode, ct);

	public Task<bool> UpdateSystemAsync(SystemData sd, CancellationToken ct = default) =>
		PostJsonAsync("/systemData", sd, ct);

	public Task<bool> UpdateDeviceAsync(DeviceData device, CancellationToken ct = default) =>
		PostJsonAsync("/device", device, ct);

	public Task<bool> SetAmbientColorAsync(string hex, CancellationToken ct = default) =>
		PostJsonAsync("/ambientColor", hex.Replace("#", ""), ct);

	public Task<bool> SetAmbientSceneAsync(int scene, CancellationToken ct = default) =>
		PostJsonAsync("/ambientScene", scene, ct);

	public Task<bool> SetAudioSceneAsync(int scene, CancellationToken ct = default) =>
		PostJsonAsync("/audioScene", scene, ct);

	public Task<bool> ScanDevicesAsync(CancellationToken ct = default) =>
		PostAsync("/scanDevices", ct);

	public async Task<HueSyncStatus?> GetHueSyncStatusAsync(CancellationToken ct = default)
	{
		try
		{
			var json = await _http.GetStringAsync(Api("/hueSyncStatus"), ct).ConfigureAwait(false);
			return JsonSerializer.Deserialize<HueSyncStatus>(json, GlimmrJson.Options);
		}
		catch
		{
			return null;
		}
	}

	public Task<bool> ArmHueSyncAsync(CancellationToken ct = default) => GetOkAsync("/hueSyncLink", ct);
	public Task<bool> UnpairHueSyncAsync(CancellationToken ct = default) => GetOkAsync("/hueSyncUnpair", ct);

	public async Task<StatData?> GetStatsAsync(CancellationToken ct = default)
	{
		try
		{
			var json = await _http.GetStringAsync(Api("/stats"), ct).ConfigureAwait(false);
			return JsonSerializer.Deserialize<StatData>(json, GlimmrJson.Options);
		}
		catch
		{
			return null;
		}
	}

	private async Task<bool> PostJsonAsync<T>(string path, T body, CancellationToken ct)
	{
		try
		{
			var content = new StringContent(JsonSerializer.Serialize(body, GlimmrJson.Options), Encoding.UTF8,
				"application/json");
			var res = await _http.PostAsync(Api(path), content, ct).ConfigureAwait(false);
			return res.IsSuccessStatusCode;
		}
		catch
		{
			return false;
		}
	}

	private async Task<bool> PostAsync(string path, CancellationToken ct)
	{
		try
		{
			var res = await _http.PostAsync(Api(path), null, ct).ConfigureAwait(false);
			return res.IsSuccessStatusCode;
		}
		catch
		{
			return false;
		}
	}

	private async Task<bool> GetOkAsync(string path, CancellationToken ct)
	{
		try
		{
			var res = await _http.GetAsync(Api(path), ct).ConfigureAwait(false);
			return res.IsSuccessStatusCode;
		}
		catch
		{
			return false;
		}
	}

	public void Dispose()
	{
		if (_ownsHttp)
		{
			_http.Dispose();
		}
	}
}
