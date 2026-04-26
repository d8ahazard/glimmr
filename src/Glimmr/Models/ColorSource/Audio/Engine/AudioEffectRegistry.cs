#region

using System;
using System.Collections.Generic;
using Glimmr.Models.ColorSource.Audio.Engine.Effects;

#endregion

namespace Glimmr.Models.ColorSource.Audio.Engine;

/// <summary>
///     Resolves a scene's <c>Mode</c> string into an <see cref="IAudioEffect" />.
///     Centralizing this means scenes can ship as plain JSON without
///     needing to know about C# class names.
/// </summary>
public static class AudioEffectRegistry {
	public const string DefaultMode = "Aurora";
	public const string LegacyMode = "Legacy";

	private static readonly Dictionary<string, Func<IAudioEffect>> Factories =
		new(StringComparer.OrdinalIgnoreCase) {
			["Aurora"] = () => new AuroraFlowEffect(),
			["AuroraFlow"] = () => new AuroraFlowEffect(),
			["BassCannon"] = () => new BassCannonEffect(),
			["LaserRain"] = () => new LaserRainEffect(),
			["SpectrumComets"] = () => new SpectrumCometsEffect(),
			["Spectrum"] = () => new SpectrumCometsEffect(),
			["ChromaBloom"] = () => new ChromaBloomEffect(),
			["Vortex"] = () => new VortexEffect(),
			["VideoPulse"] = () => new VideoPulseEffect(),
			["AudioVideo"] = () => new VideoPulseEffect(),
			["Legacy"] = () => new LegacyOctaveEffect()
		};

	public static IAudioEffect Resolve(string? mode) {
		if (!string.IsNullOrWhiteSpace(mode) && Factories.TryGetValue(mode, out var factory)) {
			return factory();
		}

		return Factories[DefaultMode]();
	}

	public static IEnumerable<string> AvailableModes => Factories.Keys;
}
