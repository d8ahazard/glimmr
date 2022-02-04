#region

using Android.App;
using Android.Content.PM;
using Android.OS;
using GlimmrControl.Core;
using Xamarin.Forms;
using Xamarin.Forms.Platform.Android;

#endregion

namespace GlimmrControl.Android {
	[Activity(Label = "Glimmr", Icon = "@drawable/LogoACh", Theme = "@style/MainTheme", MainLauncher = true,
		ConfigurationChanges = ConfigChanges.ScreenSize | ConfigChanges.Orientation)]
	public class MainActivity : FormsAppCompatActivity {
		protected override void OnCreate(Bundle savedInstanceState) {
			TabLayoutResource = Resource.Layout.Tabbar;
			ToolbarResource = Resource.Layout.Toolbar;

			base.OnCreate(savedInstanceState);
			Forms.Init(this, savedInstanceState);
			LoadApplication(new App());
		}
	}
}