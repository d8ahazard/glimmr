#region

using System.Collections.Specialized;
using System.Linq;
using Xamarin.Essentials;
using Xamarin.Forms;
using Xamarin.Forms.Xaml;

#endregion

/*
 * Glimmr App v1.0.2
 * (c) 2019 Christian Schwinne
 * Licensed under the MIT license
 * 
 * This project was build for and tested with Android, iOS and UWP.
 */

[assembly: XamlCompilation(XamlCompilationOptions.Compile)]

namespace GlimmrControl.Core {
	public partial class App : Application {
		private readonly DeviceListViewPage listview;

		private bool connectedToLocalLast;

		public App() {
			InitializeComponent();

			listview = new DeviceListViewPage();
			MainPage = listview;
			//MainPage.SetValue(NavigationPage.BarTextColorProperty, Color.White);
			Current.MainPage.SetValue(NavigationPage.BarBackgroundColorProperty, "#0000AA");

			Connectivity.ConnectivityChanged += OnConnectivityChanged;
		}

		protected override void OnStart() {
			//Directly open the device web page if connected to Glimmr Access Point
			if (NetUtility.IsConnectedToGlimmrAp()) {
				listview.OpenAPDeviceControlPage();
			}

			// Load device list from Preferences
			if (Preferences.ContainsKey("glimmrdevices")) {
				var devices = Preferences.Get("glimmrdevices", "");
				if (!devices.Equals("")) {
					var fromPreferences = Serialization.Deserialize(devices);
					if (fromPreferences != null) {
						listview.DeviceList = fromPreferences;
					}

					listview.DeviceList.CollectionChanged += SaveDevices;
				}
			}
		}

		private void SaveDevices(object sender, NotifyCollectionChangedEventArgs e) {
			Preferences.Set("glimmrdevices", Serialization.SerializeObject(listview.DeviceList));
		}

		protected override void OnSleep() {
			//Handle when app sleeps, save device list to Preferences
			var devices = Serialization.SerializeObject(listview.DeviceList);
			Preferences.Set("glimmrdevices", devices);
		}

		protected override void OnResume() {
			//Handle when app resumes, directly open the device web page if connected to Glimmr Access Point
			if (NetUtility.IsConnectedToGlimmrAp()) {
				listview.OpenAPDeviceControlPage();
			}

			//Refresh light states
			listview.RefreshAll();
		}

		private void OnConnectivityChanged(object sender, ConnectivityChangedEventArgs e) {
			//Detect if currently connected to local (WiFi) or mobile network
			var profiles = Connectivity.ConnectionProfiles;
			var connectedToLocal = profiles.Contains(ConnectionProfile.WiFi) ||
			                       profiles.Contains(ConnectionProfile.Ethernet);

			//Directly open the device web page if connected to Glimmr Access Point
			if (connectedToLocal && NetUtility.IsConnectedToGlimmrAp()) {
				listview.OpenAPDeviceControlPage();
			}

			//Refresh all devices on connection change
			if (connectedToLocal && !connectedToLocalLast) {
				listview.RefreshAll();
			}

			connectedToLocalLast = connectedToLocal;
		}
	}
}