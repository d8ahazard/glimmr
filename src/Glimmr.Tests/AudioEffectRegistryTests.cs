using System.Drawing;
using System.Linq;
using Glimmr.Models.ColorSource.Audio;
using Glimmr.Models.ColorSource.Audio.Engine;
using Xunit;

namespace Glimmr.Tests;

public class AudioEffectRegistryTests {
	[Theory]
	[InlineData("Aurora")]
	[InlineData("BassCannon")]
	[InlineData("LaserRain")]
	[InlineData("SpectrumComets")]
	[InlineData("ChromaBloom")]
	[InlineData("Vortex")]
	[InlineData("VideoPulse")]
	[InlineData("Legacy")]
	public void ResolveReturnsExpectedEffect(string mode) {
		var effect = AudioEffectRegistry.Resolve(mode);
		Assert.NotNull(effect);
	}

	[Fact]
	public void UnknownModeFallsBackToDefault() {
		var effect = AudioEffectRegistry.Resolve("nope-not-a-real-effect");
		Assert.Equal("Aurora", effect.Name);
	}

	[Theory]
	[InlineData("Aurora")]
	[InlineData("BassCannon")]
	[InlineData("LaserRain")]
	[InlineData("SpectrumComets")]
	[InlineData("ChromaBloom")]
	[InlineData("Vortex")]
	[InlineData("VideoPulse")]
	public void EveryEffectFillsAll20Slots(string mode) {
		var effect = AudioEffectRegistry.Resolve(mode);
		var output = new Color[PerimeterLayout.SlotCount];
		var bands = new float[16];
		for (var i = 0; i < bands.Length; i++) {
			bands[i] = 0.4f + 0.1f * (i % 3);
		}

		var state = new AudioReactiveState(bands, bands, 0.4f, 0.3f, 0.5f, 0.4f, 0.3f,
			0.4f, 0.6f, 0.5f, 0.3f, 0.2f, true, 120f, 0.1f,
			Enumerable.Repeat(0.5f, 12).ToArray(), 1f, 5f, 100, 0.033f);
		var scene = new AudioScene { Mode = mode, Palette = "aurora", Motion = 0.4f };

		effect.Render(state, scene, output);
		Assert.Equal(PerimeterLayout.SlotCount, output.Length);
		Assert.All(output, c => Assert.Equal(255, c.A));
	}

	[Fact]
	public void AvailableModesIncludesCanonicalNames() {
		var modes = AudioEffectRegistry.AvailableModes.ToHashSet(System.StringComparer.OrdinalIgnoreCase);
		Assert.Contains("Aurora", modes);
		Assert.Contains("BassCannon", modes);
		Assert.Contains("Legacy", modes);
	}
}
