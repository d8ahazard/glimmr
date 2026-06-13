using Microsoft.Maui.ApplicationModel;

namespace GlimmrControl.Platforms.Android;

/// <summary>
///     Runtime permission wrapper for <c>android.permission.NEARBY_WIFI_DEVICES</c> (Android 13+),
///     which replaces location as the gate for WiFi scanning.
/// </summary>
public class NearbyWifiPermission : Permissions.BasePlatformPermission
{
	public override (string androidPermission, bool isRuntime)[] RequiredPermissions =>
		new[] { ("android.permission.NEARBY_WIFI_DEVICES", true) };
}
