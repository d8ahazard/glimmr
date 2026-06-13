using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GlimmrControl.Models;
using GlimmrControl.Services;

namespace GlimmrControl.ViewModels;

/// <summary>
///     The native quick-control surface for one Glimmr server. Mirrors the web SPA's Control screen:
///     hero + mode chips, ambient/audio/stream/hue-sync panels, and per-device brightness/enable rows.
///     Backed by a live SignalR connection with REST fallback.
/// </summary>
public partial class ControlViewModel : ObservableObject, IQueryAttributable, IDisposable
{
	private readonly DeviceStore _store;
	private GlimmrConnection? _connection;
	private SystemData _system = new();
	private bool _suppress;

	[ObservableProperty] private string _deviceName = "Glimmr";
	[ObservableProperty] private string _modeName = "Off";
	[ObservableProperty] private int _mode;
	[ObservableProperty] private bool _isConnected;
	[ObservableProperty] private string _connectionStatus = "Connecting…";
	[ObservableProperty] private Color _accent = Color.FromArgb("#46C8FF");
	[ObservableProperty] private Brush _accentBrush = new SolidColorBrush(Color.FromArgb("#46C8FF"));

	[ObservableProperty] private bool _showAmbient;
	[ObservableProperty] private bool _showAudio;
	[ObservableProperty] private bool _showStream;
	[ObservableProperty] private bool _showHueSync;

	// Ambient
	[ObservableProperty] private string _ambientColorHex = "#FFFFFF";
	[ObservableProperty] private double _ambientR;
	[ObservableProperty] private double _ambientG;
	[ObservableProperty] private double _ambientB;
	[ObservableProperty] private int _ambientScene = -1;

	// Audio
	[ObservableProperty] private int _audioScene;
	[ObservableProperty] private double _audioGain;
	[ObservableProperty] private double _audioRotationSpeed;
	[ObservableProperty] private double _audioRotationTrigger;

	// Stream
	[ObservableProperty] private int _streamMode;

	// Hue Sync
	[ObservableProperty] private string _hueSyncStatus = "Not paired";

	public ControlViewModel(DeviceStore store)
	{
		_store = store;

		// Icon path data mirrors the line icons in src/Glimmr.Web/src/components/icons.tsx.
		Modes = new ObservableCollection<ChipViewModel>
		{
			new(0, "Off", "M12 2v9 M5.5 5.6a9 9 0 1 0 13 0"),
			new(1, "Video", "M4.5 5H19.5A2.5 2.5 0 0 1 22 7.5V15.5A2.5 2.5 0 0 1 19.5 18H4.5A2.5 2.5 0 0 1 2 15.5V7.5A2.5 2.5 0 0 1 4.5 5Z M8.5 21h7"),
			new(2, "Audio", "M9 18V5l11-2v13 M6 15A3 3 0 1 0 6 21A3 3 0 1 0 6 15Z M17 13A3 3 0 1 0 17 19A3 3 0 1 0 17 13Z"),
			new(4, "A/V", "M4.5 5H19.5A2.5 2.5 0 0 1 22 7.5V15.5A2.5 2.5 0 0 1 19.5 18H4.5A2.5 2.5 0 0 1 2 15.5V7.5A2.5 2.5 0 0 1 4.5 5Z M10 9.5v5l4-2.5z M8.5 21h7"),
			new(3, "Ambient", "M7.5 12A4.5 4.5 0 1 0 16.5 12A4.5 4.5 0 1 0 7.5 12Z M12 2.5v2.6M12 18.9v2.6M2.5 12h2.6M18.9 12h2.6M5 5l1.9 1.9M17.1 17.1 19 19M19 5l-1.9 1.9M6.9 17.1 5 19"),
			new(5, "Stream", "M5 12.5a7 7 0 0 1 14 0 M8.2 15a3.8 3.8 0 0 1 7.6 0 M10.4 18A1.6 1.6 0 1 0 13.6 18A1.6 1.6 0 1 0 10.4 18Z"),
			new(7, "Hue Sync", "M9 18h6 M10 21h4 M12 3a6.5 6.5 0 0 0-3.5 12V17h7v-2A6.5 6.5 0 0 0 12 3z")
		};
	}

	partial void OnAccentChanged(Color value)
	{
		AccentBrush = new SolidColorBrush(value);
	}

	public ObservableCollection<ChipViewModel> Modes { get; }
	public ObservableCollection<ChipViewModel> AmbientScenes { get; } = new();
	public ObservableCollection<ChipViewModel> AudioScenes { get; } = new();
	public ObservableCollection<DeviceRowViewModel> Devices { get; } = new();

	public GlimmrDevice? Device { get; private set; }

	public void ApplyQueryAttributes(IDictionary<string, object> query)
	{
		if (query.TryGetValue("Device", out var d) && d is GlimmrDevice device)
		{
			Device = device;
			DeviceName = device.Name;
		}
	}

	public async Task ConnectAsync()
	{
		if (Device == null || _connection != null)
		{
			return;
		}

		_connection = new GlimmrConnection(Device.BaseUrl);
		_connection.StoreReceived += OnStoreReceived;
		_connection.ModeChanged += OnHubModeChanged;
		_connection.DeviceUpdated += OnDeviceUpdated;
		_connection.DeviceDeleted += OnDeviceDeleted;
		_connection.ConnectionChanged += OnConnectionChanged;

		await _connection.StartAsync();
	}

	private void OnConnectionChanged(bool up) => MainThread.BeginInvokeOnMainThread(() =>
	{
		IsConnected = up;
		ConnectionStatus = up ? "Connected" : "Reconnecting…";
	});

	private void OnStoreReceived(StoreData store) => MainThread.BeginInvokeOnMainThread(() =>
	{
		if (store.SystemData != null)
		{
			_system = store.SystemData;
			ApplySystem(_system);
		}

		if (store.AmbientScenes != null)
		{
			SyncAmbientScenes(store.AmbientScenes);
		}

		if (store.AudioScenes != null)
		{
			SyncAudioScenes(store.AudioScenes);
		}

		if (store.Devices != null)
		{
			SyncDevices(store.Devices);
		}

		IsConnected = true;
		ConnectionStatus = "Connected";
	});

	private void OnHubModeChanged(int m) => MainThread.BeginInvokeOnMainThread(() =>
	{
		_system.DeviceMode = m;
		Mode = m;
		UpdateModeDerived();
	});

	private void OnDeviceUpdated(DeviceData dev) => MainThread.BeginInvokeOnMainThread(() =>
	{
		var row = Devices.FirstOrDefault(d => d.Id == dev.Id);
		if (row != null)
		{
			row.Apply(dev);
		}
		else if (_connection != null)
		{
			Devices.Add(new DeviceRowViewModel(dev, _connection));
		}
	});

	private void OnDeviceDeleted(string id) => MainThread.BeginInvokeOnMainThread(() =>
	{
		var row = Devices.FirstOrDefault(d => d.Id == id);
		if (row != null)
		{
			Devices.Remove(row);
		}
	});

	private void ApplySystem(SystemData sd)
	{
		_suppress = true;
		if (!string.IsNullOrEmpty(sd.DeviceName))
		{
			DeviceName = sd.DeviceName;
		}

		Mode = sd.AutoDisabled ? 0 : sd.DeviceMode;
		AmbientScene = sd.AmbientScene;
		SetAmbientHex("#" + sd.AmbientColor.Replace("#", ""));
		AudioScene = sd.AudioScene;
		AudioGain = sd.AudioGain;
		AudioRotationSpeed = sd.AudioRotationSpeed;
		AudioRotationTrigger = sd.AudioRotationTrigger;
		StreamMode = sd.StreamMode;
		_suppress = false;
		UpdateModeDerived();
		_ = RefreshHueSyncAsync();
	}

	private void UpdateModeDerived()
	{
		ModeName = Modes.FirstOrDefault(m => m.Id == Mode)?.Name ?? "Off";
		foreach (var chip in Modes)
		{
			chip.IsActive = chip.Id == Mode;
		}

		ShowAmbient = Mode == 3;
		ShowAudio = Mode is 2 or 4;
		ShowStream = Mode == 5;
		ShowHueSync = Mode == 7;
		Accent = AccentForMode(Mode, AmbientColorHex);
		PropagateAccentToChips();
		UpdateSceneActiveStates();

		if (Device != null)
		{
			Device.DeviceMode = Mode;
		}
	}

	private void UpdateSceneActiveStates()
	{
		foreach (var chip in AmbientScenes)
		{
			chip.IsActive = chip.Id == AmbientScene;
		}

		foreach (var chip in AudioScenes)
		{
			chip.IsActive = chip.Id == AudioScene;
		}
	}

	private void PropagateAccentToChips()
	{
		foreach (var chip in Modes)
		{
			chip.Accent = Accent;
		}

		foreach (var chip in AmbientScenes)
		{
			chip.Accent = Accent;
		}

		foreach (var chip in AudioScenes)
		{
			chip.Accent = Accent;
		}
	}

	private static Color AccentForMode(int mode, string ambientHex) => mode switch
	{
		0 => Color.FromArgb("#8A8F98"),
		1 => Color.FromArgb("#46C8FF"),
		2 => Color.FromArgb("#E25DFF"),
		3 => SafeColor(ambientHex, "#FFFFFF"),
		4 => Color.FromArgb("#7B6CFF"),
		5 => Color.FromArgb("#5DFF8F"),
		6 => Color.FromArgb("#5DFF8F"),
		7 => Color.FromArgb("#FF9E43"),
		_ => Color.FromArgb("#46C8FF")
	};

	private static Color SafeColor(string hex, string fallback)
	{
		try
		{
			return Color.FromArgb(hex);
		}
		catch
		{
			return Color.FromArgb(fallback);
		}
	}

	private void SetAmbientHex(string hex)
	{
		AmbientColorHex = hex;
		var c = SafeColor(hex, "#FFFFFF");
		AmbientR = Math.Round(c.Red * 255);
		AmbientG = Math.Round(c.Green * 255);
		AmbientB = Math.Round(c.Blue * 255);
	}

	private void SyncAmbientScenes(List<Scene> source)
	{
		AmbientScenes.Clear();
		AmbientScenes.Add(new ChipViewModel(-1, "Solid Color"));
		foreach (var s in source.OrderBy(x => x.Name, StringComparer.OrdinalIgnoreCase))
		{
			AmbientScenes.Add(new ChipViewModel(s.Id, s.Name));
		}

		PropagateAccentToChips();
		UpdateSceneActiveStates();
	}

	private void SyncAudioScenes(List<Scene> source)
	{
		AudioScenes.Clear();
		foreach (var s in source.OrderBy(x => x.Id))
		{
			AudioScenes.Add(new ChipViewModel(s.Id, s.Name));
		}

		PropagateAccentToChips();
		UpdateSceneActiveStates();
	}

	private void SyncDevices(List<DeviceData> source)
	{
		if (_connection == null)
		{
			return;
		}

		var ordered = source
			.Where(d => !(d.Tag == "DreamScreen" && (d.DeviceTag ?? "").Contains("DreamScreen")))
			.OrderBy(d => d.Name, StringComparer.OrdinalIgnoreCase)
			.ToList();

		Devices.Clear();
		foreach (var d in ordered)
		{
			Devices.Add(new DeviceRowViewModel(d, _connection));
		}
	}

	[RelayCommand]
	private async Task SetMode(int mode)
	{
		if (_connection == null)
		{
			return;
		}

		Mode = mode;
		_system.DeviceMode = mode;
		_system.AutoDisabled = false;
		UpdateModeDerived();
		await _connection.SetModeAsync(mode);
	}

	[RelayCommand]
	private async Task SelectAmbientScene(ChipViewModel? chip)
	{
		if (chip == null)
		{
			return;
		}

		AmbientScene = chip.Id;
		_system.AmbientScene = chip.Id;
		UpdateSceneActiveStates();
		await PushSystemAsync();
	}

	/// <summary>Commit the RGB sliders as the ambient color (and switch to a solid color).</summary>
	[RelayCommand]
	private async Task ApplyAmbientColor()
	{
		if (_suppress)
		{
			return;
		}

		var hex = $"{(int)AmbientR:X2}{(int)AmbientG:X2}{(int)AmbientB:X2}";
		AmbientColorHex = "#" + hex;
		AmbientScene = -1;
		_system.AmbientColor = hex;
		_system.AmbientScene = -1;
		Accent = AccentForMode(Mode, AmbientColorHex);
		UpdateSceneActiveStates();
		await PushSystemAsync();
	}

	[RelayCommand]
	private async Task SelectAudioScene(ChipViewModel? chip)
	{
		if (chip == null)
		{
			return;
		}

		AudioScene = chip.Id;
		_system.AudioScene = chip.Id;
		UpdateSceneActiveStates();
		await PushSystemAsync();
	}

	[RelayCommand]
	private async Task CommitAudioSettings()
	{
		if (_suppress)
		{
			return;
		}

		_system.AudioGain = (float)AudioGain;
		_system.AudioRotationSpeed = (float)AudioRotationSpeed;
		_system.AudioRotationTrigger = (int)AudioRotationTrigger;
		await PushSystemAsync();
	}

	[RelayCommand]
	private async Task SetStreamMode(int mode)
	{
		StreamMode = mode;
		_system.StreamMode = mode;
		await PushSystemAsync();
	}

	[RelayCommand]
	private async Task PairHueSync()
	{
		if (_connection == null)
		{
			return;
		}

		var ok = await _connection.ArmHueSyncAsync();
		HueSyncStatus = ok ? "Armed — open the Hue Sync app and connect within 60s" : "Failed to arm";
		await Task.Delay(1500);
		await RefreshHueSyncAsync();
	}

	[RelayCommand]
	private async Task ForgetHueSync()
	{
		if (_connection == null)
		{
			return;
		}

		await _connection.UnpairHueSyncAsync();
		await RefreshHueSyncAsync();
	}

	[RelayCommand]
	private async Task Rescan()
	{
		if (_connection != null)
		{
			await _connection.ScanDevicesAsync();
		}
	}

	[RelayCommand]
	private async Task OpenAdvanced()
	{
		if (Device == null)
		{
			return;
		}

		await Shell.Current.GoToAsync("advanced", new Dictionary<string, object> { ["Device"] = Device });
	}

	private async Task RefreshHueSyncAsync()
	{
		if (_connection == null)
		{
			return;
		}

		var status = await _connection.GetHueSyncStatusAsync();
		MainThread.BeginInvokeOnMainThread(() =>
		{
			HueSyncStatus = status == null
				? "Status unavailable"
				: status.Streaming ? "Streaming"
				: status.Paired ? "Paired"
				: status.Armed ? "Waiting for app (armed)…"
				: "Not paired";
		});
	}

	private async Task PushSystemAsync()
	{
		if (_connection != null)
		{
			await _connection.UpdateSystemAsync(_system);
		}
	}

	public void Dispose()
	{
		if (_connection != null)
		{
			_connection.StoreReceived -= OnStoreReceived;
			_connection.ModeChanged -= OnHubModeChanged;
			_connection.DeviceUpdated -= OnDeviceUpdated;
			_connection.DeviceDeleted -= OnDeviceDeleted;
			_connection.ConnectionChanged -= OnConnectionChanged;
			_ = _connection.DisposeAsync();
			_connection = null;
		}

		// persist any mode change we observed
		if (Device != null)
		{
			_store.AddOrUpdate(Device);
		}
	}
}
