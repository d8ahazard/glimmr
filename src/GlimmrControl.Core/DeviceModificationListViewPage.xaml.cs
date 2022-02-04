#region

using System;
using System.Collections.ObjectModel;
using Xamarin.Forms;
using Xamarin.Forms.Xaml;

#endregion

namespace GlimmrControl.Core {
	//Viewmodel: Page for hiding and deleting existing device list entries
	[XamlCompilation(XamlCompilationOptions.Compile)]
	public partial class DeviceModificationListViewPage : ContentPage {
		private readonly ObservableCollection<GlimmrDevice> DeviceList;

		public DeviceModificationListViewPage(ObservableCollection<GlimmrDevice> items) {
			InitializeComponent();

			DeviceList = items;
			DeviceModificationListView.ItemsSource = DeviceList;
		}

		private void OnDeleteButtonTapped(object sender, EventArgs e) {
			var s = sender as Button;
			if (!(s.Parent.BindingContext is GlimmrDevice targetDevice)) {
				return;
			}

			DeviceList.Remove(targetDevice);

			//Go back to main device list view if no devices in list
			if (DeviceList.Count == 0) {
				Navigation.PopModalAsync(false);
			}
		}

		private void OnDeviceTapped(object sender, ItemTappedEventArgs e) {
			//Deselect Item immediately
			((ListView)sender).SelectedItem = null;

			if (e.Item is GlimmrDevice targetDevice) {
				//Toggle Device enabled (disabled = hidden in list)
				targetDevice.IsEnabled = !targetDevice.IsEnabled;
			}
		}
	}
}