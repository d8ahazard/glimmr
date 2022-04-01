#region

using Newtonsoft.Json;

#endregion

namespace Glimmr.Enums;

public enum StripMode {
	/// <summary>
	///     Normal.
	/// </summary>
	[JsonProperty] Normal = 0,

	/// <summary>
	///     Sectored (use WLED segments).
	/// </summary>
	[JsonProperty] Sectored = 1,

	/// <summary>
	///     Loop colors (strip is divided in half, second half of colors are mirrored).
	/// </summary>
	[JsonProperty] Loop = 2,

	/// <summary>
	///     All leds use a single sector.
	/// </summary>
	[JsonProperty] Single = 3
}