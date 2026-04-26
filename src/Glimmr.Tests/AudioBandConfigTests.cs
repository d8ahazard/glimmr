using Glimmr.Models.ColorSource.Audio.Engine;
using Xunit;

namespace Glimmr.Tests;

public class AudioBandConfigTests {
	[Theory]
	[InlineData(AudioBandConfig.BandScale.Log)]
	[InlineData(AudioBandConfig.BandScale.Mel)]
	[InlineData(AudioBandConfig.BandScale.Octave)]
	public void BandsAreMonotonicAndCoverRange(AudioBandConfig.BandScale scale) {
		var config = AudioBandConfig.Build(16, scale, 30f, 16000f);
		Assert.Equal(16, config.BandCount);
		Assert.True(config.LowEdge[0] >= 30f * 0.99f);
		Assert.True(config.HighEdge[^1] <= 16000f * 1.01f);
		for (var i = 0; i < config.BandCount; i++) {
			Assert.True(config.HighEdge[i] > config.LowEdge[i]);
			if (i > 0) {
				Assert.True(config.LowEdge[i] >= config.LowEdge[i - 1]);
			}
		}
	}

	[Fact]
	public void TooFewBandsClampedToTwo() {
		var config = AudioBandConfig.Build(0, AudioBandConfig.BandScale.Log);
		Assert.True(config.BandCount >= 2);
	}

	[Fact]
	public void MelAndLogScalesPlaceBandsDifferentlyAtHighEnd() {
		var mel = AudioBandConfig.Build(16, AudioBandConfig.BandScale.Mel, 30f, 16000f);
		var log = AudioBandConfig.Build(16, AudioBandConfig.BandScale.Log, 30f, 16000f);

		// Mel scale becomes log-like at high frequencies but compressed
		// relative to pure log: the highest band on the log scale is wider
		// than its mel counterpart (because log keeps growing its widths
		// geometrically while mel is bounded by the perceptual model).
		var melHighWidth = mel.HighEdge[^1] - mel.LowEdge[^1];
		var logHighWidth = log.HighEdge[^1] - log.LowEdge[^1];
		Assert.True(logHighWidth > melHighWidth,
			$"Log highest-band width ({logHighWidth}) should exceed mel ({melHighWidth}).");
	}

	[Fact]
	public void OctaveScaleDoublesEachBand() {
		var oct = AudioBandConfig.Build(8, AudioBandConfig.BandScale.Octave, 32f, 8192f);
		// 8 octaves over 32->8192 (8192/32 = 256 = 2^8) => each band is one octave.
		for (var i = 0; i < oct.BandCount; i++) {
			var ratio = oct.HighEdge[i] / oct.LowEdge[i];
			Assert.InRange(ratio, 1.99f, 2.01f);
		}
	}
}
