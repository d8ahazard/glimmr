using CommunityToolkit.Maui;
using GlimmrControl.Services;
using GlimmrControl.ViewModels;
using GlimmrControl.Views;
using Microsoft.Extensions.Logging;

namespace GlimmrControl;

public static class MauiProgram
{
	public static MauiApp CreateMauiApp()
	{
		var builder = MauiApp.CreateBuilder();
		builder
			.UseMauiApp<App>()
			.UseMauiCommunityToolkit()
			.ConfigureFonts(fonts =>
			{
				fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
				fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
			});

		// Services
		builder.Services.AddSingleton<DeviceStore>();
		builder.Services.AddSingleton<DiscoveryService>();
		builder.Services.AddSingleton<OnboardingService>();

#if ANDROID
		builder.Services.AddSingleton<IWifiService, Platforms.Android.WifiService>();
#elif IOS
		builder.Services.AddSingleton<IWifiService, Platforms.iOS.WifiService>();
#elif WINDOWS
		builder.Services.AddSingleton<IWifiService, Platforms.Windows.WifiService>();
#else
		builder.Services.AddSingleton<IWifiService, NullWifiService>();
#endif

		// ViewModels
		builder.Services.AddSingleton<DeviceListViewModel>();
		builder.Services.AddTransient<OnboardingViewModel>();
		builder.Services.AddTransient<ControlViewModel>();

		// Pages
		builder.Services.AddSingleton<DeviceListPage>();
		builder.Services.AddTransient<OnboardingPage>();
		builder.Services.AddTransient<ControlPage>();
		builder.Services.AddTransient<AdvancedSettingsPage>();

#if DEBUG
		builder.Logging.AddDebug();
#endif

		return builder.Build();
	}
}
