#region

using Newtonsoft.Json;

#endregion

namespace Glimmr.Enums;

/// <summary>
///     How a 2D matrix target maps the screen onto the panel. Only Mirror and Extend are exposed;
///     Blend/Column are legacy values kept for back-compat and treated as Mirror.
/// </summary>
public enum MatrixMode {
	/// <summary>Legacy band-spread mode; treated as <see cref="Mirror" />.</summary>
	[JsonProperty] Blend = 0,

	/// <summary>Legacy single-strip mode; treated as <see cref="Mirror" />.</summary>
	[JsonProperty] Column = 1,

	/// <summary>
	///     Literal per-pixel mirror of the screen content onto the panel.
	/// </summary>
	[JsonProperty] Mirror = 2,

	/// <summary>
	///     Reflect the screen's near edge outward across the panel, fading with distance, so the
	///     panel looks like a continuation/reflection of the screen rather than a flat stretch.
	/// </summary>
	[JsonProperty] Extend = 3
}
