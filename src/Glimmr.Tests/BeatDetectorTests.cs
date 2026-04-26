using Glimmr.Models.ColorSource.Audio.Engine;
using Xunit;

namespace Glimmr.Tests;

public class BeatDetectorTests {
	[Fact]
	public void NoBeatOnSilence() {
		var det = new BeatDetector(16);
		var quiet = new float[16];
		var beats = 0;
		for (var i = 0; i < 60; i++) {
			var (beat, _) = det.Process(quiet, 0.033f, 0.6f);
			if (beat) {
				beats++;
			}
		}

		Assert.Equal(0, beats);
	}

	[Fact]
	public void NoBeatOnSustainedTone() {
		var det = new BeatDetector(16);
		var tone = new float[16];
		for (var i = 0; i < 16; i++) {
			tone[i] = 0.4f;
		}

		var beats = 0;
		// First few frames may register because we're going from 0 to 0.4
		// "all at once" - that IS an onset. Skip the priming frames.
		for (var i = 0; i < 30; i++) {
			det.Process(tone, 0.033f, 0.6f);
		}

		for (var i = 0; i < 60; i++) {
			var (beat, _) = det.Process(tone, 0.033f, 0.6f);
			if (beat) {
				beats++;
			}
		}

		Assert.Equal(0, beats);
	}

	[Fact]
	public void DetectsBeatsOnImpulseSequence() {
		var det = new BeatDetector(16);
		var quiet = new float[16];
		var loud = new float[16];
		for (var i = 0; i < 16; i++) {
			loud[i] = 0.6f;
		}

		var beats = 0;
		for (var seq = 0; seq < 30; seq++) {
			// Send 6 frames quiet, 1 frame loud, repeating.
			for (var q = 0; q < 6; q++) {
				det.Process(quiet, 0.033f, 0.6f);
			}

			var (beat, _) = det.Process(loud, 0.033f, 0.6f);
			if (beat) {
				beats++;
			}
		}

		Assert.True(beats >= 5, $"Expected several beats, got {beats}");
	}
}
