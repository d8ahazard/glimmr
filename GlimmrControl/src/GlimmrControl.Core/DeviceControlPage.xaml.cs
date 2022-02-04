#region

using System;
using Xamarin.Forms;
using Xamarin.Forms.Xaml;

#endregion

namespace GlimmrControl.Core {
	//Viewmodel: Open a web view that loads the mobile UI natively hosted on Glimmr device
	[XamlCompilation(XamlCompilationOptions.Compile)]
	public partial class DeviceControlPage : ContentPage {
		private readonly GlimmrDevice currentDevice;

		public DeviceControlPage(string pageURL, GlimmrDevice device) {
			InitializeComponent();
			currentDevice = device;
			if (currentDevice == null) {
				loadingLabel.Text =
					"Loading... (Glimmr-AP)"; //If the device is null, we are connected to the Glimmr light's access point
			}

			UIBrowser.Source = pageURL;
			UIBrowser.Navigated += OnNavigationCompleted;
			topMenuBar.LeftButtonTapped += OnBackButtonTapped;
		}

		private void OnNavigationCompleted(object sender, WebNavigatedEventArgs e) {
			if (e.Result == WebNavigationResult.Success) {
				loadingLabel.IsVisible = false;
				if (currentDevice != null) {
					currentDevice.CurrentStatus = DeviceStatus.Default;
				}
			} else {
				if (currentDevice != null) {
					currentDevice.CurrentStatus = DeviceStatus.Unreachable;
				}

				loadingLabel.IsVisible = true;
				loadingLabel.Text = "Device Unreachable";
			}
		}

		private async void OnBackButtonTapped(object sender, EventArgs e) {
			await Navigation.PopModalAsync(false);
			currentDevice?.Refresh(); //refresh device list item to apply changes made in the control page
		}
	}
}