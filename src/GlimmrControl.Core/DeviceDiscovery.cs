#region

using System;
using Tmds.MDns;

#endregion

namespace GlimmrControl.Core {
	//Discover _http._tcp services via mDNS/Zeroconf and verify they are Glimmr devices by sending an API call
	internal class DeviceDiscovery {
		private static DeviceDiscovery _instance;
		private readonly ServiceBrowser serviceBrowser;

		private DeviceDiscovery() {
			serviceBrowser = new ServiceBrowser();
			serviceBrowser.ServiceAdded += OnServiceAdded;
		}

		public event EventHandler<DeviceCreatedEventArgs> ValidDeviceFound;

		public void StartDiscovery() {
			serviceBrowser.StartBrowse("_glimmr._tcp");
		}

		public void StopDiscovery() {
			serviceBrowser.StopBrowse();
		}

		private async void OnServiceAdded(object sender, ServiceAnnouncementEventArgs e) {
			var toAdd = new GlimmrDevice();
			foreach (var address in e.Announcement.Addresses) {
				toAdd.NetworkAddress = address.ToString();
				break; //only get first address
			}

			toAdd.Name = e.Announcement.Hostname;
			toAdd.NameIsCustom = false;
			if (await toAdd.Refresh()) //check if the service is a valid Glimmr device
			{
				OnValidDeviceFound(new DeviceCreatedEventArgs(toAdd, false));
			}
		}

		public static DeviceDiscovery GetInstance() {
			return _instance ?? (_instance = new DeviceDiscovery());
		}

		protected virtual void OnValidDeviceFound(DeviceCreatedEventArgs e) {
			ValidDeviceFound?.Invoke(this, e);
		}
	}
}