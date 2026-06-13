using System.Text.Json.Serialization;
using CommunityToolkit.Mvvm.ComponentModel;

namespace GlimmrControl.Models;

public enum DeviceReachability
{
	Unknown,
	Online,
	Offline
}

/// <summary>
///     A Glimmr server the app knows about - either discovered via mDNS or manually added.
///     Persisted to <c>Preferences</c> and surfaced in the device list. Observable so the list
///     reflects live reachability and mode changes.
/// </summary>
public partial class GlimmrDevice : ObservableObject
{
	/// <summary>Host or IP, e.g. "192.168.1.42" or "glimmr-1234.local".</summary>
	[ObservableProperty] private string _host = "";

	[ObservableProperty] private string _name = "Glimmr";

	/// <summary>Stable server device id (from SystemData), used for de-duplication when known.</summary>
	[ObservableProperty] private string? _deviceId;

	[JsonIgnore] [ObservableProperty] private int _deviceMode;

	[JsonIgnore]
	[ObservableProperty]
	[NotifyPropertyChangedFor(nameof(StatusText))]
	private DeviceReachability _reachability = DeviceReachability.Unknown;

	public GlimmrDevice()
	{
	}

	public GlimmrDevice(string host, string name)
	{
		Host = host;
		Name = name;
	}

	/// <summary>Base http URL for REST + hub connections.</summary>
	[JsonIgnore]
	public string BaseUrl => Host.StartsWith("http", StringComparison.OrdinalIgnoreCase) ? Host : $"http://{Host}";

	[JsonIgnore]
	public string StatusText => Reachability switch
	{
		DeviceReachability.Online => Host,
		DeviceReachability.Offline => $"{Host} (offline)",
		_ => Host
	};
}
