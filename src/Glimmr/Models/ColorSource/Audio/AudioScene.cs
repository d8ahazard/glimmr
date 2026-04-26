#region

using System.Collections.Generic;
using Newtonsoft.Json;

#endregion

namespace Glimmr.Models.ColorSource.Audio;

/// <summary>
///     A persisted preset for the audio visualizer engine. The struct
///     intentionally still carries the legacy octave-map fields so older
///     user scenes deserialize without loss; new fields default to
///     sensible values that drive the modern engine.
/// </summary>
public struct AudioScene {
	#region Legacy

	/// <summary>Lower bound of the legacy hue rotation range (0..1).</summary>
	[JsonProperty]
	public float RotationLower { get; set; }

	/// <summary>Upper bound of the legacy hue rotation range (0..1).</summary>
	[JsonProperty]
	public float RotationUpper { get; set; }

	/// <summary>Legacy hue rotation step per trigger (0..1).</summary>
	[JsonProperty]
	public float RotationSpeed { get; set; }

	/// <summary>Legacy beat threshold; 0..1 or 0..255 are both accepted.</summary>
	[JsonProperty]
	public float RotationThreshold { get; set; }

	/// <summary>Legacy region -> octave map. Still used by the Legacy effect.</summary>
	[JsonProperty]
	public Dictionary<string, int> OctaveMap { get; set; }

	#endregion

	[JsonProperty] public int Id { get; set; }
	[JsonProperty] public string Name { get; set; }
	[JsonProperty] public bool System { get; set; }

	#region Engine

	/// <summary>
	///     The visualizer effect identifier. See <c>AudioEffectRegistry</c>
	///     for the full list. Empty falls back to <c>Aurora</c>; old scenes
	///     without this field auto-route to <c>Legacy</c>.
	/// </summary>
	[JsonProperty]
	public string Mode { get; set; }

	/// <summary>
	///     Built-in palette name (e.g. <c>aurora</c>, <c>fire</c>) or a
	///     comma-separated hex color list (e.g. <c>#102030,#ffaa00</c>).
	/// </summary>
	[JsonProperty]
	public string Palette { get; set; }

	/// <summary>
	///     Frequency band scaling: <c>Log</c>, <c>Mel</c>, or <c>Octave</c>.
	/// </summary>
	[JsonProperty]
	public string BandScale { get; set; }

	/// <summary>Number of analysis bands. 8..64 supported, default 16.</summary>
	[JsonProperty]
	public int BandCount { get; set; }

	/// <summary>User gain (0..1). Layered on top of AGC's allowed maximum.</summary>
	[JsonProperty]
	public float Sensitivity { get; set; }

	/// <summary>Hard noise gate (0..1). 0 = AGC-only, higher = blacker silence.</summary>
	[JsonProperty]
	public float NoiseGate { get; set; }

	/// <summary>Attack smoothing (0..1). Higher = bands snap to peaks faster.</summary>
	[JsonProperty]
	public float Attack { get; set; }

	/// <summary>Decay smoothing (0..1). Higher = bands fall off faster.</summary>
	[JsonProperty]
	public float Decay { get; set; }

	/// <summary>Beat-detector sensitivity multiplier (0..1, default ~0.6).</summary>
	[JsonProperty]
	public float BeatSensitivity { get; set; }

	/// <summary>Effect-specific 'motion' factor (0..1) for flow speed.</summary>
	[JsonProperty]
	public float Motion { get; set; }

	/// <summary>Effect-specific sparkle factor (0..1).</summary>
	[JsonProperty]
	public float Sparkle { get; set; }

	/// <summary>0 = full strobe allowed, 1 = strobe heavily limited.</summary>
	[JsonProperty]
	public float StrobeLimit { get; set; }

	#endregion

	/// <summary>
	///     True when the scene is a legacy preset (no modern <see cref="Mode" />
	///     specified but a populated <see cref="OctaveMap" />). Such scenes
	///     route through <c>LegacyOctaveEffect</c>.
	/// </summary>
	[JsonIgnore]
	public bool IsLegacy => string.IsNullOrWhiteSpace(Mode) && OctaveMap is { Count: > 0 };

	/// <summary>Effective <see cref="Mode" />, applying legacy fall-back logic.</summary>
	[JsonIgnore]
	public string EffectiveMode => string.IsNullOrWhiteSpace(Mode)
		? OctaveMap is { Count: > 0 } ? "Legacy" : "Aurora"
		: Mode;
}
