using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GlimmrControl.Models;
using GlimmrControl.Services;

namespace GlimmrControl.ViewModels;

public partial class DeviceListViewModel : ObservableObject
{
	private readonly DiscoveryService _discovery;
	private readonly DeviceStore _store;

	[ObservableProperty] private bool _isScanning;
	[ObservableProperty] private string? _statusMessage;

	public DeviceListViewModel(DeviceStore store, DiscoveryService discovery)
	{
		_store = store;
		_discovery = discovery;
		_store.Load();
	}

	public ObservableCollection<GlimmrDevice> Devices => _store.Devices;

	public bool HasNoDevices => Devices.Count == 0;

	[RelayCommand]
	private async Task Appearing()
	{
		OnPropertyChanged(nameof(HasNoDevices));
		await RefreshReachabilityAsync();
	}

	[RelayCommand]
	private async Task Scan()
	{
		if (IsScanning)
		{
			return;
		}

		IsScanning = true;
		StatusMessage = "Searching for Glimmr devices…";
		try
		{
			var found = await _discovery.ScanAsync(TimeSpan.FromSeconds(6));
			foreach (var device in found)
			{
				_store.AddOrUpdate(device);
			}

			await RefreshReachabilityAsync();
			StatusMessage = found.Count == 0
				? "No devices found. Make sure you're on the same Wi-Fi, or add one by IP."
				: $"Found {found.Count} device(s).";
		}
		finally
		{
			IsScanning = false;
			OnPropertyChanged(nameof(HasNoDevices));
		}
	}

	[RelayCommand]
	private async Task AddManual()
	{
		var ip = await Shell.Current.DisplayPromptAsync(
			"Add Glimmr",
			"Enter the device's IP address or hostname:",
			accept: "Add",
			cancel: "Cancel",
			placeholder: "192.168.1.42",
			keyboard: Keyboard.Url);

		if (string.IsNullOrWhiteSpace(ip))
		{
			return;
		}

		var device = new GlimmrDevice(ip.Trim(), ip.Trim());
		using var api = new GlimmrApiClient(device.BaseUrl);
		var sd = await api.GetSystemDataAsync();
		if (sd == null)
		{
			await Shell.Current.DisplayAlert("Not found",
				$"Couldn't reach a Glimmr device at {ip}.", "OK");
			return;
		}

		device.Name = string.IsNullOrEmpty(sd.DeviceName) ? device.Host : sd.DeviceName;
		device.DeviceId = sd.DeviceId;
		device.DeviceMode = sd.DeviceMode;
		device.Reachability = DeviceReachability.Online;
		_store.AddOrUpdate(device);
		OnPropertyChanged(nameof(HasNoDevices));
	}

	[RelayCommand]
	private async Task Open(GlimmrDevice? device)
	{
		if (device == null)
		{
			return;
		}

		await Shell.Current.GoToAsync("control", new Dictionary<string, object> { ["Device"] = device });
	}

	[RelayCommand]
	private async Task Remove(GlimmrDevice? device)
	{
		if (device == null)
		{
			return;
		}

		var confirm = await Shell.Current.DisplayAlert("Remove device",
			$"Remove \"{device.Name}\" from the list?", "Remove", "Cancel");
		if (confirm)
		{
			_store.Remove(device);
			OnPropertyChanged(nameof(HasNoDevices));
		}
	}

	[RelayCommand]
	private async Task StartOnboarding()
	{
		await Shell.Current.GoToAsync("onboarding");
	}

	private async Task RefreshReachabilityAsync()
	{
		var tasks = Devices.Select(async device =>
		{
			using var api = new GlimmrApiClient(device.BaseUrl);
			var sd = await api.GetSystemDataAsync();
			if (sd != null)
			{
				device.Reachability = DeviceReachability.Online;
				device.DeviceMode = sd.DeviceMode;
				if (!string.IsNullOrEmpty(sd.DeviceName))
				{
					device.Name = sd.DeviceName;
				}

				if (string.IsNullOrEmpty(device.DeviceId) && !string.IsNullOrEmpty(sd.DeviceId))
				{
					device.DeviceId = sd.DeviceId;
				}
			}
			else
			{
				device.Reachability = DeviceReachability.Offline;
			}
		});

		await Task.WhenAll(tasks);
	}
}
