#region

using System;
using Xamarin.Essentials;
using Xamarin.Forms.Xaml;

#endregion

namespace GlimmrControl.Core {
	public enum ButtonLocation {
		Left,
		Right
	}

	public enum ButtonIcon {
		None,
		Back,
		Add,
		Delete,
		Done
	}

	//View Element: Custom menu bar present on all content pages
	[XamlCompilation(XamlCompilationOptions.Compile)]
	public partial class MenuBar {
		public MenuBar() {
			InitializeComponent();
		}

		public event EventHandler LeftButtonTapped, RightButtonTapped;

		public void SetButtonIcon(ButtonLocation loc, ButtonIcon ico) {
			var path = "";

			switch (ico) {
				case ButtonIcon.Back:
					path = "icon_back.png";
					break;
				case ButtonIcon.Add:
					path = "icon_add.png";
					break;
				case ButtonIcon.Delete:
					path = "icon_bin.png";
					break;
				case ButtonIcon.Done:
					path = "icon_check.png";
					break;
			}

			if (loc == ButtonLocation.Left) {
				ImageLeft.Source = path;
			} else {
				ImageRight.Source = path;
			}
		}

		private void OnLogoTapped(object sender, EventArgs eventArgs) {
			Launcher.OpenAsync(new Uri("https://github.com/d8ahazard/Glimmr"));
		}

		protected virtual void OnLeftButtonTapped(object sender, EventArgs eventArgs) {
			var handler = LeftButtonTapped;
			if (handler != null) {
				handler(this, eventArgs);
			} else {
				Navigation.PopModalAsync(false);
			}
		}

		protected virtual void OnRightButtonTapped(object sender, EventArgs eventArgs) {
			RightButtonTapped?.Invoke(this, eventArgs);
		}
	}
}