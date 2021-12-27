#region

using System;
using Newtonsoft.Json;

#endregion

namespace Glimmr.Models.ColorSource.Ambient; 

[Serializable]
public class AmbientScene {
	/// <summary>
	///     Amount of time (in milliseconds) between color updates.
	/// </summary>
	[JsonProperty]
	public float AnimationTime { get; set; }

	/// <summary>
	///     How long to ease between colors.
	/// </summary>
	[JsonProperty]
	public float EasingTime { get; set; }

	/// <summary>
	///     Theme ID. Will be auto-assigned for user defined themes.
	/// </summary>
	[JsonProperty]
	public int Id { get; set; }

	/// <summary>
	///     Easing mode.
	///     Blend = 0 (Colors fade directly between one another)
	///     FadeIn = 1 (Color fades in after being replaced)
	///     FadeOut = 2 (Color fades out, is replaced, turns on full)
	///     FadeInOut = 3 (Colors fade in/out before being replaced)
	/// </summary>
	[JsonProperty]
	public string Easing { get; set; } = "Blend";

	/// <summary>
	///     Animation mode.
	///     Linear = 0 (Colors progress normally)
	///     Reverse = 1 (Colors progress in reverse direction)
	///     Random = 2 (Colors are selected randomly)
	///     RandomAll = 3 (One random color for everything)
	///     LinearAll = 4 (Colors progress normally, one color for everything)
	/// </summary>
	[JsonProperty]
	public string Mode { get; set; } = "Linear";

	/// <summary>
	///     The theme name.
	/// </summary>
	[JsonProperty]
	public string Name { get; set; } = "";

	/// <summary>
	///     An array of colors used by the scene.
	/// </summary>
	[JsonProperty]
	public string[] Colors { get; set; } = Array.Empty<string>();
}