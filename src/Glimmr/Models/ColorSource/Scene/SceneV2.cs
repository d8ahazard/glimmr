#region

using System.Collections.Generic;
using System.ComponentModel;
using Newtonsoft.Json;

#endregion

namespace Glimmr.Models.ColorSource.Scene;

/// <summary>
///     A user-tunable knob for an animation scene. The UI renders one slider per param; the
///     current value (defaulting to <see cref="Default" />) is substituted into a script scene's
///     <c>${Key}</c> tokens or handed to a built-in provider before playback.
/// </summary>
public class SceneParam {
	/// <summary>Stable identifier referenced by scripts (<c>${Key}</c>) and providers.</summary>
	[JsonProperty]
	public string Key { get; set; } = "";

	/// <summary>Human-friendly slider label.</summary>
	[JsonProperty]
	public string Label { get; set; } = "";

	[JsonProperty] public double Min { get; set; }
	[JsonProperty] public double Max { get; set; } = 1;
	[JsonProperty] public double Step { get; set; } = 1;
	[JsonProperty] public double Default { get; set; }

	/// <summary>Optional one-line explanation shown under the slider.</summary>
	[JsonProperty]
	public string Hint { get; set; } = "";
}

/// <summary>
///     A "Scenes" tab scene. Unlike the legacy <see cref="Ambient.AmbientScene" /> (a single
///     scrolling image), a <see cref="SceneV2" /> is frame-driven: either a live scripted
///     particle animation (<see cref="Kind" /> = "script") or a baked video frame sequence
///     (<see cref="Kind" /> = "video"). Stored as JSON in the <c>scenes</c> directory; video
///     frames live in a sibling gzipped binary blob named <see cref="FramesFile" />.
/// </summary>
public class SceneV2 {
	/// <summary>
	///     Unique id (GUID for user scenes, a stable slug for bundled system scenes).
	/// </summary>
	[JsonProperty]
	public string Id { get; set; } = "";

	/// <summary>
	///     Display name.
	/// </summary>
	[JsonProperty]
	public string Name { get; set; } = "";

	/// <summary>
	///     One of: "color" (solid color), "image" (a scrolling color matrix, e.g. Image2Scene
	///     output), "video" (baked frame sequence), or "script" (live particle animation).
	/// </summary>
	[DefaultValue("script")]
	[JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
	public string Kind { get; set; } = "script";

	/// <summary>
	///     Built-in scene shipped with Glimmr. System scenes can't be overwritten or deleted by users.
	/// </summary>
	[DefaultValue(false)]
	[JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
	public bool System { get; set; }

	/// <summary>
	///     Render grid width in cells (columns). Matched to the curtain/matrix where possible.
	/// </summary>
	[DefaultValue(32)]
	[JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
	public int Width { get; set; } = 32;

	/// <summary>
	///     Render grid height in cells (rows).
	/// </summary>
	[DefaultValue(48)]
	[JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
	public int Height { get; set; } = 48;

	/// <summary>
	///     Playback / simulation frame rate.
	/// </summary>
	[DefaultValue(30d)]
	[JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
	public double Fps { get; set; } = 30;

	/// <summary>
	///     Whether a video sequence loops.
	/// </summary>
	[DefaultValue(true)]
	[JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
	public bool Loop { get; set; } = true;

	/// <summary>
	///     Whether a video sequence ping-pongs (plays forward then backward) when looping.
	/// </summary>
	[DefaultValue(false)]
	[JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
	public bool Reverse { get; set; }

	/// <summary>
	///     Force a horizontally-mirrored (symmetric) display on matrices: the left half is
	///     reflected onto the right half before the grid is sent.
	/// </summary>
	[DefaultValue(false)]
	[JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
	public bool Mirror { get; set; }

	/// <summary>
	///     Solid color (hex) for <see cref="Kind" /> = "color". Empty falls back to the system
	///     ambient color, so the colour picker can drive it live.
	/// </summary>
	[JsonProperty]
	public string Color { get; set; } = "";

	/// <summary>
	///     2D hex color grid for <see cref="Kind" /> = "image" (rows of columns, top-left origin).
	/// </summary>
	[JsonProperty(DefaultValueHandling = DefaultValueHandling.Ignore)]
	public string[][]? ColorMatrix { get; set; }

	/// <summary>
	///     Scroll direction for image scenes: Random, LTR, TTB, RTL, BTT, CW, CCW.
	/// </summary>
	[DefaultValue("Random")]
	[JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
	public string MatrixDirection { get; set; } = "Random";

	/// <summary>
	///     Cells the image scrolls per <see cref="AnimationTime" /> for image scenes.
	/// </summary>
	[DefaultValue(1)]
	[JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
	public int MatrixStep { get; set; } = 1;

	/// <summary>
	///     Seconds the image takes to scroll <see cref="MatrixStep" /> cells.
	/// </summary>
	[DefaultValue(3f)]
	[JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
	public float AnimationTime { get; set; } = 3f;

	/// <summary>
	///     The animation script (DSL text) for <see cref="Kind" /> = "script".
	/// </summary>
	[JsonProperty]
	public string Script { get; set; } = "";

	/// <summary>
	///     File name (relative to the scene's directory) of the gzipped frame blob for
	///     <see cref="Kind" /> = "video". Each frame is Width*Height*3 RGB bytes, row-major top-left.
	/// </summary>
	[JsonProperty]
	public string FramesFile { get; set; } = "";

	/// <summary>
	///     Number of frames in the baked video blob.
	/// </summary>
	[DefaultValue(0)]
	[JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
	public int FrameCount { get; set; }

	/// <summary>
	///     Name of the built-in animation provider for <see cref="Kind" /> = "builtin"
	///     (e.g. "tetris", "pacman", "lightning"). Empty for script/video/image/color scenes.
	/// </summary>
	[JsonProperty]
	public string Provider { get; set; } = "";

	/// <summary>
	///     Tunable sliders this scene exposes. Script scenes reference them as <c>${Key}</c>
	///     tokens; built-in providers receive the resolved values. Empty for scenes with no knobs.
	/// </summary>
	[JsonProperty]
	public List<SceneParam> Params { get; set; } = new();
}
