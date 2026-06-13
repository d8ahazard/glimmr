using GlimmrControl.Models;

namespace GlimmrControl.Views;

[QueryProperty(nameof(Device), "Device")]
public partial class AdvancedSettingsPage : ContentPage
{
	private GlimmrDevice? _device;

	public AdvancedSettingsPage()
	{
		InitializeComponent();
		Web.Navigated += OnNavigated;
	}

	public GlimmrDevice? Device
	{
		get => _device;
		set
		{
			_device = value;
			if (value != null)
			{
				Title = value.Name;
				Web.Source = value.BaseUrl;
			}
		}
	}

	private void OnNavigated(object? sender, WebNavigatedEventArgs e)
	{
		Spinner.IsRunning = false;
		Spinner.IsVisible = false;
	}
}
