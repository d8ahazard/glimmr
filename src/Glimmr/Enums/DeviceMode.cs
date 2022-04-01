#region

using Newtonsoft.Json;

#endregion

namespace Glimmr.Enums;

/// <summary>
///     The current device mode.
///     Off = 0
///     Video = 1
///     Audio = 2
///     Ambient = 3
///     AudioVideo = 4
///     Udp = 5
///     DreamScreen = 6
/// </summary>
public enum DeviceMode {
	/// <summary>
	///     Off
	/// </summary>
	[JsonProperty] Off = 0,

	/// <summary>
	///     Video
	/// </summary>
	[JsonProperty] Video = 1,

	/// <summary>
	///     Audio
	/// </summary>
	[JsonProperty] Audio = 2,

	/// <summary>
	///     Ambient
	/// </summary>
	[JsonProperty] Ambient = 3,

	/// <summary>
	///     Audio+Video
	/// </summary>
	[JsonProperty] AudioVideo = 4,

	/// <summary>
	///     UDP (Glimmr/WLED)
	/// </summary>
	[JsonProperty] Udp = 5,

	/// <summary>
	///     DreamScreen
	/// </summary>
	[JsonProperty] DreamScreen = 6
}