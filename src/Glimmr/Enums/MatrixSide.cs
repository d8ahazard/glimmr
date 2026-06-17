#region

using Newtonsoft.Json;

#endregion

namespace Glimmr.Enums;

/// <summary>
///     Which screen edge a 2D matrix panel sits next to. Fixes the panel's "near edge"
///     (closest to the screen, showing that edge's colors) and the direction effects fan out.
/// </summary>
public enum MatrixSide {
	/// <summary>
	///     Panel is to the left of the screen. Sample axis vertical, spread along X.
	/// </summary>
	[JsonProperty] Left = 0,

	/// <summary>
	///     Panel is to the right of the screen. Sample axis vertical, spread along X.
	/// </summary>
	[JsonProperty] Right = 1,

	/// <summary>
	///     Panel is above the screen. Sample axis horizontal, spread along Y.
	/// </summary>
	[JsonProperty] Top = 2,

	/// <summary>
	///     Panel is below the screen. Sample axis horizontal, spread along Y.
	/// </summary>
	[JsonProperty] Bottom = 3
}
