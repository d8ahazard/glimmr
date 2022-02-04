#region

using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Xml.Serialization;
using Xamarin.Forms;
using Xamarin.Forms.Xaml;

#endregion

namespace GlimmrControl.Core {
	//ViewModel: Main device list page with power button and brightness slider per device
	[Serializable]
	[XmlRoot(Namespace = "", IsNullable = false)]
	[XamlCompilation(XamlCompilationOptions.Compile)]
	public partial class DeviceListViewPage : ContentPage {
		public ObservableCollection<GlimmrDevice> DeviceList {
			set {
				deviceList = value;
				DeviceListView.ItemsSource = deviceList;
				RefreshAll();
				UpdateElementsVisibility();
			}
			get => deviceList;
		}

		private ObservableCollection<GlimmrDevice> deviceList;


		public DeviceListViewPage() {
			InitializeComponent();

			DeviceList = new ObservableCollection<GlimmrDevice>();

			topMenuBar.SetButtonIcon(ButtonLocation.Left, ButtonIcon.Delete);
			topMenuBar.SetButtonIcon(ButtonLocation.Right, ButtonIcon.Add);
			topMenuBar.LeftButtonTapped += OnDeletionModeButtonTapped;
			topMenuBar.RightButtonTapped += OnAddButtonTapped;
		}


		private void OnRefresh(object sender, EventArgs e) {
			RefreshAll();
			DeviceListView.EndRefresh();
		}

		private async void OnAddButtonTapped(object sender, EventArgs e) {
			var page = new DeviceAddPage(this);
			page.DeviceCreated += OnDeviceCreated;
			await Navigation.PushModalAsync(page, false);
		}

		private async void OnDeletionModeButtonTapped(object sender, EventArgs e) {
			if (deviceList.Count == 0) {
				return; //do not enter deletion view if there are no devices
			}

			var page = new DeviceModificationListViewPage(deviceList);
			await Navigation.PushModalAsync(page, false);
		}

		private void OnDeviceCreated(object sender, DeviceCreatedEventArgs e) {
			var toAdd = e.CreatedDevice;

			if (toAdd != null) {
				foreach (var d in deviceList) {
					//ensure there is only one device entry per IP
					if (toAdd.NetworkAddress.Equals(d.NetworkAddress)) {
						if (toAdd.NameIsCustom) {
							d.Name = toAdd.Name;
							d.NameIsCustom = true;
							ReinsertDeviceSorted(d);
						}

						return;
					}
				}

				InsertDeviceSorted(toAdd);

				toAdd.PropertyChanged += DevicePropertyChanged;
				if (e.RefreshRequired) {
					_ = toAdd.Refresh();
				}

				UpdateElementsVisibility();
			}
		}

		//Re-Insert device in list if its name changed to maintain alphabetical sorting
		private void DevicePropertyChanged(object sender, PropertyChangedEventArgs e) {
			if (e.PropertyName.Equals("Name")) {
				ReinsertDeviceSorted(sender as GlimmrDevice);
			}
		}

		private void InsertDeviceSorted(GlimmrDevice d) {
			var index = 0;
			while (index < deviceList.Count && d.CompareTo(deviceList.ElementAt(index)) > 0) {
				index++;
			}

			deviceList.Insert(index, d);
		}

		private void ReinsertDeviceSorted(GlimmrDevice d) {
			if (d == null) {
				return;
			}

			if (deviceList.Remove(d)) {
				InsertDeviceSorted(d);
			}
		}

		private void ModeTapped(object sender, EventArgs eventArgs) {
			var s = sender as ModeButton;

			if (s.Parent.BindingContext is GlimmrDevice targetDevice) {
				var mode = s.Mode;
				targetDevice.DeviceMode = mode;
				_ = targetDevice.SendApiCall("/mode", "?mode=" + mode);
			}
		}


		protected override void OnAppearing() {
			//Returning from deletion or add pages
			UpdateElementsVisibility();
		}

		private async void OnDeviceTapped(object sender, ItemTappedEventArgs e) {
			//Deselect Item immediately
			((ListView)sender).SelectedItem = null;

			if (!(e.Item is GlimmrDevice targetDevice)) {
				return;
			}

			var url = "http://" + targetDevice.NetworkAddress;

			//Open web UI control page
			var page = new DeviceControlPage(url, targetDevice);
			await Navigation.PushModalAsync(page, false);
		}

		internal async void OpenAPDeviceControlPage() {
			var page = new DeviceControlPage("http://10.41.0.1", null);
			await Navigation.PushModalAsync(page, false);
		}

		private void
			UpdateElementsVisibility() //Show welcome labels and hide deletion mode button if there are no devices
		{
			var listIsEmpty = deviceList.Count == 0;

			welcomeLabel.IsVisible = listIsEmpty;
			instructionLabel.IsVisible = listIsEmpty;
			topMenuBar.SetButtonIcon(ButtonLocation.Left, listIsEmpty ? ButtonIcon.None : ButtonIcon.Delete);

			//iOS workaround for listview not updating unless ItemSource is modified
			if (Device.RuntimePlatform == Device.iOS) {
				var dummy = new GlimmrDevice();
				deviceList.Add(dummy);
				deviceList.Remove(dummy);
			}
		}

		internal void RefreshAll() {
			foreach (var d in deviceList) {
				_ = d.Refresh();
			}
		}
	}
}