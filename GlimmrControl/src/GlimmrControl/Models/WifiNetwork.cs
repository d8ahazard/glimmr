namespace GlimmrControl.Models;

/// <summary>
///     A scanned WiFi network. Used both for nearby <c>glimmr-*</c> setup hotspots (platform scan)
///     and for the home networks reported by comitup-web while the phone is on the setup AP.
/// </summary>
public class WifiNetwork
{
	public string Ssid { get; set; } = "";

	/// <summary>Signal strength, 0-100. -1 when unknown.</summary>
	public int Strength { get; set; } = -1;

	/// <summary>True if the network is encrypted and therefore needs a password.</summary>
	public bool Secured { get; set; }

	/// <summary>True for networks whose SSID matches the Glimmr setup hotspot pattern.</summary>
	public bool IsGlimmrSetup { get; set; }
}
