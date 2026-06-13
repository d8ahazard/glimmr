namespace GlimmrControl.Models;

/// <summary>
///     Mirrors Glimmr.Enums.DeviceMode on the server. Values are wire-stable.
/// </summary>
public enum DeviceMode
{
	Off = 0,
	Video = 1,
	Audio = 2,
	Ambient = 3,
	AudioVideo = 4,
	Udp = 5,
	DreamScreen = 6,
	HueSync = 7
}
