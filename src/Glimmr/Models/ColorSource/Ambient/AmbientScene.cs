#region

using System;
using Newtonsoft.Json;

#endregion

namespace Glimmr.Models.ColorSource.Ambient;

[Serializable]
public class AmbientScene {
	/// <summary>
	///     If the scene is system-defined or not. If it is, it cannot be deleted or overwritten.
	/// </summary>
	/// [JsonProperty]
	public bool System { get; set; } = false;

	/// <summary>
	///     Amount of time (in milliseconds) between color updates.
	/// </summary>
	[JsonProperty]
	public float AnimationTime { get; set; }

	/// <summary>
	///     Deprecated. This is now determined by easing type and animation time.
	/// </summary>
	[JsonProperty(DefaultValueHandling = DefaultValueHandling.Ignore)]
	public float EasingTime { get; set; }

	/// <summary>
	///     Theme ID. Will be auto-assigned for user defined themes.
	/// </summary>
	[JsonProperty]
	public int Id { get; set; }

	/// <summary>
	///     How many spaces to animate in a given direction per step.
	/// </summary>
	[JsonProperty]
	public int MatrixStep { get; set; } = 1;

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
	///     Matrix = 5 (Uses a custom grid to create more advanced animations)
	/// </summary>
	[JsonProperty]
	public string Mode { get; set; } = "Linear";

	/// <summary>
	///     The theme name.
	/// </summary>
	[JsonProperty]
	public string Name { get; set; } = "";

	/// <summary>
	///     The animation direction of the color matrix.
	/// </summary>
	[JsonProperty]
	public string? MatrixDirection { get; set; }

	/// <summary>
	///     An array of colors used by the scene.
	/// </summary>
	[JsonProperty]
	public string[]? Colors { get; set; } = Array.Empty<string>();

	/// <summary>
	///     A 2D array of colors used by the scene (basically, a small image)
	/// </summary>
	[JsonProperty]
	public string[][]? ColorMatrix { get; set; }

	public AmbientScene() { }

	public AmbientScene(string name, string[][] toArray, string md, float delay, int step) {
		Name = name;
		ColorMatrix = toArray;
		MatrixDirection = md;
		AnimationTime = delay;
		MatrixStep = step;
		Mode = "Matrix";
		Easing = "Blend";
		Id = 0;
		Colors = Array.Empty<string>();
	}
}