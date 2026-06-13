using System.Text.Json;
using GlimmrControl.Models;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.DependencyInjection;

namespace GlimmrControl.Services;

/// <summary>
///     A live session against one Glimmr server: a SignalR hub at <c>/socket</c> for push updates
///     plus a <see cref="GlimmrApiClient" /> for actions, with REST used as a fallback whenever the
///     hub isn't connected. Mirrors the web SPA's store.ts behavior.
/// </summary>
public sealed class GlimmrConnection : IAsyncDisposable
{
	private readonly GlimmrApiClient _api;
	private readonly HubConnection _hub;

	public GlimmrConnection(string baseUrl)
	{
		BaseUrl = baseUrl.TrimEnd('/');
		_api = new GlimmrApiClient(BaseUrl);

		_hub = new HubConnectionBuilder()
			.WithUrl($"{BaseUrl}/socket")
			.WithAutomaticReconnect(new[]
			{
				TimeSpan.Zero, TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(2),
				TimeSpan.FromSeconds(4), TimeSpan.FromSeconds(8)
			})
			.AddJsonProtocol(o =>
			{
				o.PayloadSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
				o.PayloadSerializerOptions.PropertyNameCaseInsensitive = true;
			})
			.Build();

		_hub.ServerTimeout = TimeSpan.FromSeconds(60);
		_hub.KeepAliveInterval = TimeSpan.FromSeconds(10);

		_hub.On<StoreData>("olo", store => StoreReceived?.Invoke(store));
		_hub.On<int>("mode", m => ModeChanged?.Invoke(m));
		_hub.On<StatData>("stats", s => StatsReceived?.Invoke(s));
		_hub.On<string>("device", json =>
		{
			try
			{
				var dev = JsonSerializer.Deserialize<DeviceData>(json, GlimmrJson.Options);
				if (dev != null && !string.IsNullOrEmpty(dev.Id))
				{
					DeviceUpdated?.Invoke(dev);
				}
			}
			catch
			{
				// ignore malformed payloads
			}
		});
		_hub.On<string>("deleteDevice", id => DeviceDeleted?.Invoke(id));

		_hub.Reconnecting += _ =>
		{
			ConnectionChanged?.Invoke(false);
			return Task.CompletedTask;
		};
		_hub.Reconnected += async _ =>
		{
			ConnectionChanged?.Invoke(true);
			await SafeInvoke("Store").ConfigureAwait(false);
		};
		_hub.Closed += _ =>
		{
			ConnectionChanged?.Invoke(false);
			return Task.CompletedTask;
		};
	}

	public string BaseUrl { get; }
	public bool IsHubConnected => _hub.State == HubConnectionState.Connected;

	public event Action<StoreData>? StoreReceived;
	public event Action<int>? ModeChanged;
	public event Action<DeviceData>? DeviceUpdated;
	public event Action<string>? DeviceDeleted;
	public event Action<StatData>? StatsReceived;
	public event Action<bool>? ConnectionChanged;

	/// <summary>
	///     Start the hub. On success the server immediately pushes the full store ("olo").
	///     On failure, falls back to a one-shot REST store fetch so the UI still populates.
	/// </summary>
	public async Task StartAsync(CancellationToken ct = default)
	{
		try
		{
			await _hub.StartAsync(ct).ConfigureAwait(false);
			ConnectionChanged?.Invoke(true);
		}
		catch
		{
			ConnectionChanged?.Invoke(false);
			await RefreshViaRestAsync(ct).ConfigureAwait(false);
		}
	}

	/// <summary>Pull the full store over REST (used when the hub is down).</summary>
	public async Task RefreshViaRestAsync(CancellationToken ct = default)
	{
		var store = await _api.GetStoreAsync(ct).ConfigureAwait(false);
		if (store != null)
		{
			StoreReceived?.Invoke(store);
		}
	}

	public async Task SetModeAsync(int mode)
	{
		if (!await SafeInvoke("Mode", mode).ConfigureAwait(false))
		{
			await _api.SetModeAsync(mode).ConfigureAwait(false);
		}
	}

	public async Task UpdateSystemAsync(SystemData sd)
	{
		var json = JsonSerializer.Serialize(sd, GlimmrJson.Options);
		if (!await SafeInvoke("SystemData", json).ConfigureAwait(false))
		{
			await _api.UpdateSystemAsync(sd).ConfigureAwait(false);
		}
	}

	public async Task UpdateDeviceAsync(DeviceData device)
	{
		var json = JsonSerializer.Serialize(device, GlimmrJson.Options);
		if (!await SafeInvoke("UpdateDevice", json).ConfigureAwait(false))
		{
			await _api.UpdateDeviceAsync(device).ConfigureAwait(false);
		}
	}

	public async Task DeleteDeviceAsync(string id)
	{
		if (!await SafeInvoke("DeleteDevice", id).ConfigureAwait(false))
		{
			// no REST fallback needed beyond hub; ignore failure
		}
	}

	public async Task ScanDevicesAsync()
	{
		if (!await SafeInvoke("ScanDevices").ConfigureAwait(false))
		{
			await _api.ScanDevicesAsync().ConfigureAwait(false);
		}
	}

	// Ambient color/scene and audio scene also set the device mode server-side, so the
	// dedicated REST endpoints are used (the hub has no equivalent single-call method).
	public Task SetAmbientColorAsync(string hex) => _api.SetAmbientColorAsync(hex);
	public Task SetAmbientSceneAsync(int scene) => _api.SetAmbientSceneAsync(scene);
	public Task SetAudioSceneAsync(int scene) => _api.SetAudioSceneAsync(scene);

	public Task<HueSyncStatus?> GetHueSyncStatusAsync() => _api.GetHueSyncStatusAsync();
	public Task<bool> ArmHueSyncAsync() => _api.ArmHueSyncAsync();
	public Task<bool> UnpairHueSyncAsync() => _api.UnpairHueSyncAsync();

	private async Task<bool> SafeInvoke(string method, params object?[] args)
	{
		if (_hub.State != HubConnectionState.Connected)
		{
			return false;
		}

		try
		{
			await _hub.InvokeCoreAsync(method, args).ConfigureAwait(false);
			return true;
		}
		catch
		{
			return false;
		}
	}

	public async ValueTask DisposeAsync()
	{
		try
		{
			await _hub.DisposeAsync().ConfigureAwait(false);
		}
		catch
		{
			// ignore
		}

		_api.Dispose();
	}
}
