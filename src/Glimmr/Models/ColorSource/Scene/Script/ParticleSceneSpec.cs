#region

using System;
using System.Collections.Generic;
using System.Drawing;

#endregion

namespace Glimmr.Models.ColorSource.Scene.Script;

/// <summary>
///     The compiled form of an animation script. Produced by <see cref="SceneScriptParser" />
///     and executed by <see cref="ParticleEngine" />. Kept deliberately small - this is an
///     Atari-era particle system, not a shader graph.
/// </summary>
public class ParticleSceneSpec {
	public int Width { get; set; } = 32;
	public int Height { get; set; } = 48;
	public double Fps { get; set; } = 30;
	public Color Background { get; set; } = Color.Black;

	/// <summary>
	///     Per-frame persistence of the light field (0 = no trails, &lt;1 = fading trails). A
	///     value near 0.85 leaves comet-like tails behind moving particles.
	/// </summary>
	public double Trail { get; set; } = 0.85;

	/// <summary>
	///     All emitters by name.
	/// </summary>
	public Dictionary<string, EmitterSpec> Emitters { get; } = new(StringComparer.OrdinalIgnoreCase);

	/// <summary>
	///     Names of emitters that spawn continuously from the scene itself (those with a rate).
	/// </summary>
	public List<string> RootEmitters { get; } = new();
}

/// <summary>
///     A closed numeric range. A single value is represented as Min == Max.
/// </summary>
public readonly struct NumRange {
	public readonly double Min;
	public readonly double Max;

	public NumRange(double min, double max) {
		Min = min;
		Max = max;
	}

	public NumRange(double v) : this(v, v) {
	}

	public double Sample(Random r) {
		return Max <= Min ? Min : Min + r.NextDouble() * (Max - Min);
	}

	public int SampleInt(Random r) {
		return (int)Math.Round(Sample(r));
	}
}

public enum SpawnDirection {
	None,
	Up,
	Down,
	Left,
	Right,
	Radial
}

public enum ColorMode {
	Fixed,
	Pick,
	Random,
	Rainbow,
	Parent
}

public enum OriginAxis {
	/// <summary>Fixed normalized position (0..1).</summary>
	Fixed,

	/// <summary>Random across the axis.</summary>
	Random
}

public class OriginSpec {
	public bool FromParent { get; set; }
	public OriginAxis XMode { get; set; } = OriginAxis.Fixed;
	public double X { get; set; } = 0.5;
	public OriginAxis YMode { get; set; } = OriginAxis.Fixed;
	public double Y { get; set; } = 1.0;
}

public class EmitterSpec {
	public string Name { get; set; } = "";

	// Spawning ---------------------------------------------------------------
	/// <summary>Continuous (rate-driven, root) vs burst (spawned by another emitter).</summary>
	public bool Continuous { get; set; }

	/// <summary>Seconds between spawn events for a continuous emitter.</summary>
	public NumRange RateInterval { get; set; } = new(0.8);

	/// <summary>Particles emitted per spawn event (continuous) or per burst.</summary>
	public NumRange Count { get; set; } = new(1);

	// Motion -----------------------------------------------------------------
	public OriginSpec Origin { get; set; } = new();
	public SpawnDirection Direction { get; set; } = SpawnDirection.Up;
	public NumRange Speed { get; set; } = new(20);

	/// <summary>Gravity acceleration in cells/sec^2 (x, y); y positive points down.</summary>
	public double GravityX { get; set; }
	public double GravityY { get; set; }

	// Appearance -------------------------------------------------------------
	public ColorMode ColorMode { get; set; } = ColorMode.Fixed;
	public Color Color { get; set; } = Color.White;
	public List<Color> Palette { get; } = new();

	/// <summary>Optional hue rotation in degrees/sec applied over a particle's life.</summary>
	public double HueShift { get; set; }

	// Lifetime ---------------------------------------------------------------
	/// <summary>Lifetime range in seconds. Particles fade out over their life.</summary>
	public NumRange Life { get; set; } = new(1.0);

	/// <summary>Die at the apex of an upward arc (when vertical velocity reverses).</summary>
	public bool DieOnApex { get; set; }

	// Death behaviour --------------------------------------------------------
	/// <summary>Emitter to spawn at this particle's position when it dies.</summary>
	public string? SpawnOnDeath { get; set; }

	/// <summary>Probability (0..1) that <see cref="SpawnOnDeath" /> fires.</summary>
	public double SpawnChance { get; set; } = 1.0;
}
