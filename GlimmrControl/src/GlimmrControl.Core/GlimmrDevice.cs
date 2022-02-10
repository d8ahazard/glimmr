#region

using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Xml.Serialization;
using GlimmrControl.Core.Models;
using Newtonsoft.Json;
using Xamarin.Forms;

#endregion

namespace GlimmrControl.Core {
	internal enum DeviceStatus {
		Default,
		Unreachable,
		Error
	}

	//Data Model. Represents a Glimmr light with a network address, name, and some current light values.
	[XmlType("dev")]
	public class GlimmrDevice : INotifyPropertyChanged, IComparable {
		[XmlElement("url")]
		public string NetworkAddress {
			set {
				if (value == null || value.Length < 3) {
					return; //More elaborate checking for URL syntax could be added here
				}

				networkAddress = value;
			}
			get => networkAddress;
		}

		[XmlElement("name")]
		public string Name {
			set {
				if (value == null || name.Equals(value)) {
					return; //Make sure name is not set to null
				}

				name = value;
				OnPropertyChanged("Name");
			}
			get => name;
		}

		[XmlElement("ncustom")]
		public bool NameIsCustom { get; set; } =
			true; //If the light name is custom, the name returned by the API response will be ignored

		[XmlElement("en")]
		public bool IsEnabled {
			set {
				isEnabled = value;
				OnPropertyChanged("Status");
				OnPropertyChanged("ListHeight");
				OnPropertyChanged("TextColor");
				OnPropertyChanged("IsEnabled");
			}
			get => isEnabled;
		}


		[XmlIgnore]
		public Color AmbientColor =>
			DeviceMode == 3 ? Color.FromHex("#666") : Color.FromHex("#222"); //button background color

		[XmlIgnore]
		public Color AudioColor =>
			DeviceMode == 2 ? Color.FromHex("#666") : Color.FromHex("#222"); //button background color

		[XmlIgnore]
		public Color AvColor =>
			DeviceMode == 4 ? Color.FromHex("#666") : Color.FromHex("#222"); //button background color

		[XmlIgnore]
		public Color PowerColor =>
			DeviceMode == 0 ? Color.FromHex("#666") : Color.FromHex("#222"); //button background color

		[XmlIgnore]
		public Color StreamColor =>
			DeviceMode == 5 ? Color.FromHex("#666") : Color.FromHex("#222"); //button background color

		[XmlIgnore]
		public Color VideoColor =>
			DeviceMode == 1 ? Color.FromHex("#666") : Color.FromHex("#222"); //button background color


		[XmlIgnore]
		public int DeviceMode {
			get => deviceMode;
			set {
				OnPropertyChanged("PowerColor");
				OnPropertyChanged("VideoColor");
				OnPropertyChanged("AudioColor");
				OnPropertyChanged("AvColor");
				OnPropertyChanged("AmbientColor");
				OnPropertyChanged("StreamColor");
				deviceMode = value;
			}
		}

		[XmlIgnore]
		public string ListHeight => isEnabled ? "-1" : "0"; //height of one view cell (set to 0 to hide device)


		[XmlIgnore]
		public string Status //string containing IP and current status, second label in list viewcell
		{
			get {
				var statusText = "";
				if (IsEnabled) {
					switch (status) {
						case DeviceStatus.Default:
							statusText = "";
							break;
						case DeviceStatus.Unreachable:
							statusText = " (Offline)";
							break;
						case DeviceStatus.Error:
							statusText = " (Error)";
							break;
					}
				} else {
					statusText = " (Hidden)";
				}

				return $"{networkAddress}{statusText}";
			}
		}

		[XmlIgnore] public string TextColor => isEnabled ? "#FFF" : "#999"; //text color for modification page

		internal DeviceStatus CurrentStatus {
			set {
				status = value;
				OnPropertyChanged("Status");
			}
			get => status;
		}

		private int deviceMode;
		private bool isEnabled = true;
		private string name = ""; //device display name ("Server Description")
		private string networkAddress = "10.41.0.1"; //device IP (can also be hostname if applicable)
		private DeviceStatus status = DeviceStatus.Default; //Current connection status

		//constructors
		public GlimmrDevice() {
		}

		public GlimmrDevice(string nA, string name) {
			NetworkAddress = nA;
			Name = name;
		}

		public int CompareTo(object comp) //compares devices in alphabetic order based on name
		{
			var c = comp as GlimmrDevice;
			if (c == null || c.Name == null) {
				return 1;
			}

			var result = name.CompareTo(c.name);
			if (result != 0) {
				return result;
			}

			return networkAddress.CompareTo(c.networkAddress);
		}

		public event PropertyChangedEventHandler PropertyChanged;

		//member functions

		//send a call to this device's Glimmr HTTP API
		public async Task<bool> SendApiCall(string call, string p = "") {
			var url = "http://" + networkAddress;
			if (networkAddress.StartsWith("https://")) {
				url = networkAddress;
			}

			Debug.WriteLine("URL: " + url);
			var response = await DeviceHttpConnection.GetInstance().Send_Glimmr_API_Call(url, call + p);
			if (response == null) {
				Debug.WriteLine("NO RESPONSE.");
				CurrentStatus = DeviceStatus.Unreachable;
				return false;
			}

			if (
				response.Equals(
					"err")) //404 or other non-success http status codes, indicates that target is not a Glimmr device
			{
				Debug.WriteLine("Response error.");
				CurrentStatus = DeviceStatus.Error;
				return false;
			}

			var deviceResponse = JsonConvert.DeserializeObject<SystemData>(response);
			if (deviceResponse == null) //could not parse XML API response
			{
				Debug.WriteLine("Response is null.");
				CurrentStatus = DeviceStatus.Error;
				return false;
			}

			Debug.WriteLine("We have a valid response: " + JsonConvert.SerializeObject(deviceResponse));

			CurrentStatus = DeviceStatus.Default; //the received response was valid

			if (!NameIsCustom) {
				Name = deviceResponse.DeviceName;
			}

			DeviceMode = (int)deviceResponse.DeviceMode;
			Debug.WriteLine("Returning true?");
			return true;
		}

		public async Task<bool> Refresh() //fetches updated values from Glimmr device
		{
			if (!IsEnabled) {
				return false;
			}

			return await SendApiCall("");
		}

		protected virtual void OnPropertyChanged(string propertyName) {
			PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
		}
	}
}