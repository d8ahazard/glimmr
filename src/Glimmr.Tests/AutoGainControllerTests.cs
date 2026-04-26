using Glimmr.Models.ColorSource.Audio.Engine;
using Xunit;

namespace Glimmr.Tests;

public class AutoGainControllerTests {
	[Fact]
	public void GainRampsUpForQuietSignal() {
		var agc = new AutoGainController();
		var bands = new float[8];
		for (var i = 0; i < 8; i++) {
			bands[i] = 0.05f;
		}

		var initial = agc.Gain;
		for (var i = 0; i < 60; i++) {
			var b = (float[])bands.Clone();
			agc.Process(0.05f, b, 0.033f, 1f);
		}

		Assert.True(agc.Gain > initial,
			$"Gain should rise for a quiet signal; gain={agc.Gain}, initial={initial}");
	}

	[Fact]
	public void GainPullsDownForLoudSignal() {
		var agc = new AutoGainController();
		// First settle on a loud signal so initial gain ramps high.
		var bands = new float[8];
		for (var i = 0; i < 8; i++) {
			bands[i] = 0.2f;
		}

		for (var i = 0; i < 30; i++) {
			var b = (float[])bands.Clone();
			agc.Process(0.2f, b, 0.033f, 1f);
		}

		var afterMedium = agc.Gain;

		// Now slam it with a much louder signal: gain should drop quickly.
		for (var i = 0; i < 8; i++) {
			bands[i] = 0.95f;
		}

		for (var i = 0; i < 60; i++) {
			var b = (float[])bands.Clone();
			agc.Process(0.95f, b, 0.033f, 1f);
		}

		Assert.True(agc.Gain < afterMedium,
			$"Gain should drop for a loud signal; before={afterMedium}, after={agc.Gain}");
	}

	[Fact]
	public void NoiseGateClosesOnSilence() {
		var agc = new AutoGainController();
		var bands = new float[8];

		for (var i = 0; i < 60; i++) {
			var b = (float[])bands.Clone();
			agc.Process(0f, b, 0.033f, 1f);
		}

		Assert.Equal(0f, agc.Gate, 3);
	}

	[Fact]
	public void NoiseGateOpensOnAudio() {
		var agc = new AutoGainController();
		var bands = new float[8];
		for (var i = 0; i < 8; i++) {
			bands[i] = 0.4f;
		}

		for (var i = 0; i < 60; i++) {
			var b = (float[])bands.Clone();
			agc.Process(0.4f, b, 0.033f, 1f);
		}

		Assert.True(agc.Gate > 0.9f, $"Gate should be open; got {agc.Gate}");
	}

	[Fact]
	public void OutputBandsAreClampedToOne() {
		var agc = new AutoGainController(target: 0.65f, minGain: 1f, maxGain: 100f);
		var bands = new float[4];
		for (var i = 0; i < 4; i++) {
			bands[i] = 0.5f;
		}

		// Use peak that drives gain toward an extreme.
		for (var i = 0; i < 200; i++) {
			var b = (float[])bands.Clone();
			agc.Process(0.5f, b, 0.033f, 1f);
			foreach (var v in b) {
				Assert.InRange(v, 0f, 1f);
			}
		}
	}
}
