using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GlimmrControl.Models;
using GlimmrControl.Services;

namespace GlimmrControl.ViewModels;

public enum OnboardingStep
{
	Intro,
	FindHotspot,
	Portal,
	Provisioning
}

/// <summary>
///     Drives the WiFi onboarding wizard. Steps: find/join the <c>glimmr-*</c> setup hotspot ->
///     pick the home network and submit credentials via comitup -> reconnect and rediscover the
///     now-provisioned Glimmr.
/// </summary>
public partial class OnboardingViewModel : ObservableObject
{
	private readonly OnboardingService _onboarding;
	private readonly DeviceStore _store;

	[ObservableProperty] private bool _isBusy;
	[ObservableProperty] private string _statusMessage = "";
	[ObservableProperty] private string _manualSsid = "glimmr-";

	[ObservableProperty]
	[NotifyPropertyChangedFor(nameof(ShowIntro), nameof(ShowFindHotspot), nameof(ShowPortal),
		nameof(ShowProvisioning), nameof(CanEnterSsidManually))]
	private OnboardingStep _step = OnboardingStep.Intro;

	public OnboardingViewModel(OnboardingService onboarding, DeviceStore store)
	{
		_onboarding = onboarding;
		_store = store;
	}

	public ObservableCollection<WifiNetwork> Hotspots { get; } = new();
	public ObservableCollection<WifiNetwork> HomeNetworks { get; } = new();

	public bool CanScan => _onboarding.CanScanForHotspots;
	public bool CanEnterSsidManually => Step == OnboardingStep.FindHotspot && !CanScan;

	public bool ShowIntro => Step == OnboardingStep.Intro;
	public bool ShowFindHotspot => Step == OnboardingStep.FindHotspot;
	public bool ShowPortal => Step == OnboardingStep.Portal;
	public bool ShowProvisioning => Step == OnboardingStep.Provisioning;

	[RelayCommand]
	private async Task Start()
	{
		IsBusy = true;
		StatusMessage = "Requesting Wi-Fi permissions…";
		var granted = await _onboarding.EnsurePermissionsAsync();
		IsBusy = false;

		if (!granted && CanScan)
		{
			StatusMessage = "Wi-Fi permission is required to find your Glimmr.";
			return;
		}

		Step = OnboardingStep.FindHotspot;
		StatusMessage = "";

		// If we're already sitting on the setup hotspot, jump straight to the portal.
		if (await _onboarding.WaitForPortalAsync(TimeSpan.FromSeconds(2)))
		{
			await EnterPortalAsync();
			return;
		}

		if (CanScan)
		{
			await ScanHotspots();
		}
	}

	[RelayCommand]
	private async Task ScanHotspots()
	{
		if (!CanScan)
		{
			return;
		}

		IsBusy = true;
		StatusMessage = "Looking for Glimmr setup networks…";
		Hotspots.Clear();
		var found = await _onboarding.FindSetupHotspotsAsync();
		foreach (var net in found)
		{
			Hotspots.Add(net);
		}

		IsBusy = false;
		StatusMessage = found.Count == 0
			? "No Glimmr setup network found. Power on your Glimmr and wait ~30s, then scan again."
			: "";
	}

	[RelayCommand]
	private Task JoinHotspot(WifiNetwork? net) =>
		net == null ? Task.CompletedTask : JoinSsidAsync(net.Ssid);

	[RelayCommand]
	private async Task JoinManual()
	{
		if (string.IsNullOrWhiteSpace(ManualSsid))
		{
			return;
		}

		await JoinSsidAsync(ManualSsid.Trim());
	}

	private async Task JoinSsidAsync(string ssid)
	{
		IsBusy = true;
		StatusMessage = $"Joining {ssid}…";
		var joined = await _onboarding.JoinSetupHotspotAsync(ssid);
		IsBusy = false;

		if (!joined)
		{
			StatusMessage = $"Couldn't connect to {ssid}. Join it from your Wi-Fi settings, then tap \"I'm connected\".";
			return;
		}

		await EnterPortalAsync();
	}

	/// <summary>Manual escape hatch: user joined the AP themselves via OS Wi-Fi settings.</summary>
	[RelayCommand]
	private async Task AlreadyConnected()
	{
		IsBusy = true;
		StatusMessage = "Checking for the Glimmr setup portal…";
		var ok = await _onboarding.WaitForPortalAsync(TimeSpan.FromSeconds(8));
		IsBusy = false;

		if (ok)
		{
			await EnterPortalAsync();
		}
		else
		{
			StatusMessage = "Still can't reach the setup portal. Make sure you're connected to the glimmr-… network.";
		}
	}

	private async Task EnterPortalAsync()
	{
		Step = OnboardingStep.Portal;
		await RefreshHomeNetworks();
	}

	[RelayCommand]
	private async Task RefreshHomeNetworks()
	{
		IsBusy = true;
		StatusMessage = "Reading available Wi-Fi networks…";
		HomeNetworks.Clear();
		var nets = await _onboarding.ListHomeNetworksAsync();
		foreach (var n in nets)
		{
			HomeNetworks.Add(n);
		}

		IsBusy = false;
		StatusMessage = nets.Count == 0 ? "No networks found yet. Pull to refresh." : "";
	}

	[RelayCommand]
	private async Task SelectHomeNetwork(WifiNetwork? net)
	{
		if (net == null)
		{
			return;
		}

		var password = "";
		if (net.Secured)
		{
			password = await Shell.Current.DisplayPromptAsync(
				net.Ssid, "Enter the Wi-Fi password:", accept: "Connect", cancel: "Cancel",
				placeholder: "Password");
			if (password == null)
			{
				return; // cancelled
			}
		}

		IsBusy = true;
		StatusMessage = $"Sending Glimmr the credentials for {net.Ssid}…";
		var ok = await _onboarding.SubmitHomeCredentialsAsync(net.Ssid, password);
		IsBusy = false;

		if (!ok)
		{
			StatusMessage = "Couldn't submit credentials. Make sure you're still on the Glimmr setup network.";
			return;
		}

		Step = OnboardingStep.Provisioning;
		StatusMessage = $"Glimmr is joining {net.Ssid}. Reconnect this phone to {net.Ssid}, then tap the button below.";
	}

	[RelayCommand]
	private async Task FindProvisionedDevice()
	{
		IsBusy = true;
		StatusMessage = "Searching for your Glimmr on the network…";
		var device = await _onboarding.WaitForProvisionedDeviceAsync(TimeSpan.FromSeconds(45));
		IsBusy = false;

		if (device == null)
		{
			StatusMessage = "Couldn't find it yet. Give it a moment to boot, make sure your phone is back on home Wi-Fi, and try again.";
			return;
		}

		_store.AddOrUpdate(device);
		StatusMessage = $"Found {device.Name}!";
		await Shell.Current.GoToAsync("//devices");
	}

	/// <summary>
	///     Fallback: open the comitup captive portal in the system browser so the user can pick
	///     their network manually if the in-app native flow has trouble.
	/// </summary>
	[RelayCommand]
	private async Task OpenPortalInBrowser()
	{
		try
		{
			await Launcher.OpenAsync(ComitupClient.PortalUrl);
		}
		catch
		{
			StatusMessage = "Couldn't open the setup page. Browse to http://10.41.0.1 manually.";
		}
	}

	[RelayCommand]
	private async Task Cancel()
	{
		await Shell.Current.GoToAsync("//devices");
	}
}
