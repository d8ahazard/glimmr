#region

using System.Collections.Generic;
using Newtonsoft.Json;

#endregion

namespace Glimmr.Models.ColorSource.Audio;

public struct AudioScene {
	/// <summary>
	///     Overall lower limit to color range (0 - 1)
	///     If lower is GEQ higher, will be ignored
	/// </summary>
	[JsonProperty]
	public float RotationLower { get; set; }

	/// <summary>
	///     Overall upper limit to color range (0 - 1)
	///     If lower is GEQ higher, will be ignored
	/// </summary>
	[JsonProperty]
	public float RotationUpper { get; set; }

	/// <summary>
	///     How many degrees to rotate on each trigger (0 - 1)
	/// </summary>
	[JsonProperty]
	public float RotationSpeed { get; set; }

	/// <summary>
	///     Minimum amplitude to trigger color rotation
	/// </summary>
	[JsonProperty]
	public float RotationThreshold { get; set; }

	[JsonProperty] public Dictionary<string, int> OctaveMap { get; set; }
	[JsonProperty] public int Id { get; set; }
	[JsonProperty] public string Name { get; set; }
}