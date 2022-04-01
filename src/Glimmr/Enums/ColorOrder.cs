#region

using Newtonsoft.Json;

#endregion

namespace Glimmr.Enums;

public enum ColorOrder {
	/// <summary>
	///     RGB
	/// </summary>
	[JsonProperty] Rgb = 0,
	[JsonProperty] RGB = 0,

	/// <summary>
	///     RBG
	/// </summary>
	[JsonProperty] Rbg = 1,
	[JsonProperty] RBG = 1,

	/// <summary>
	///     GBR
	/// </summary>
	[JsonProperty] Gbr = 2,
	[JsonProperty] GBR = 2,

	/// <summary>
	///     GRB
	/// </summary>
	[JsonProperty] Grb = 3,
	[JsonProperty] GRB = 3,

	/// <summary>
	///     BGR
	/// </summary>
	[JsonProperty] Bgr = 4,
	[JsonProperty] BGR = 4,

	/// <summary>
	///     BRG
	/// </summary>
	[JsonProperty] Brg = 5,
	[JsonProperty] BRG = 5
}