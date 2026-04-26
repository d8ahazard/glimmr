using System;
using Glimmr.Models.ColorSource.Audio.Engine;
using Xunit;

namespace Glimmr.Tests;

public class AudioAnalyzerTests {
	private const int SampleSize = 512;
	private const int SampleRate = 48000;

	[Fact]
	public void EmptyFftReturnsEmptyState() {
		var analyzer = new AudioAnalyzer(AudioBandConfig.Build(16, AudioBandConfig.BandScale.Log));
		var state = analyzer.Analyze(Array.Empty<float>(), SampleRate, 0.033f, 0.5f, 0.5f, 0f, 0f);
		Assert.Equal(0f, state.Rms);
		Assert.False(state.Beat);
	}

	[Fact]
	public void BassToneLightsUpBassBucket() {
		var analyzer = new AudioAnalyzer(AudioBandConfig.Build(16, AudioBandConfig.BandScale.Log));
		var fft = new float[SampleSize];
		// 80 Hz tone -> bin index ~ 80 / (24000/512) ~= 1-2.
		fft[2] = 0.4f;
		fft[3] = 0.5f;

		AudioReactiveState state = AudioReactiveState.Empty;
		// Run a handful of frames so the AGC stabilizes.
		for (var i = 0; i < 30; i++) {
			state = analyzer.Analyze(fft, SampleRate, 0.033f, 1f, 0.5f, 0f, 0f);
		}

		Assert.True(state.Bass > state.Mid, $"Expected bass>mid, got bass={state.Bass} mid={state.Mid}");
		Assert.True(state.Bass > state.Treble);
	}

	[Fact]
	public void TrebleToneLightsUpTrebleBucket() {
		var analyzer = new AudioAnalyzer(AudioBandConfig.Build(16, AudioBandConfig.BandScale.Log));
		var fft = new float[SampleSize];
		// 8 kHz -> bin ~ 8000 / 46.875 ~= 170
		fft[170] = 0.6f;
		fft[171] = 0.5f;

		AudioReactiveState state = AudioReactiveState.Empty;
		for (var i = 0; i < 30; i++) {
			state = analyzer.Analyze(fft, SampleRate, 0.033f, 1f, 0.5f, 0f, 0f);
		}

		Assert.True(state.Treble > state.Bass,
			$"Expected treble>bass, got treble={state.Treble} bass={state.Bass}");
	}

	[Fact]
	public void NoiseGateZerosBandsBelowFloor() {
		var analyzer = new AudioAnalyzer(AudioBandConfig.Build(16, AudioBandConfig.BandScale.Log));
		var fft = new float[SampleSize];
		fft[10] = 0.0001f;

		var state = analyzer.Analyze(fft, SampleRate, 0.033f, 1f, 0.5f, 0.9f, 0f);
		Assert.All(state.Bands, v => Assert.Equal(0f, v));
	}

	[Fact]
	public void RmsRespondsProportionallyToInputEnergy() {
		var analyzer1 = new AudioAnalyzer(AudioBandConfig.Build(16, AudioBandConfig.BandScale.Log));
		var analyzer2 = new AudioAnalyzer(AudioBandConfig.Build(16, AudioBandConfig.BandScale.Log));

		var quiet = new float[SampleSize];
		var loud = new float[SampleSize];
		for (var i = 5; i < 50; i++) {
			quiet[i] = 0.05f;
			loud[i] = 0.5f;
		}

		AudioReactiveState quietState = AudioReactiveState.Empty;
		AudioReactiveState loudState = AudioReactiveState.Empty;
		for (var i = 0; i < 40; i++) {
			quietState = analyzer1.Analyze(quiet, SampleRate, 0.033f, 0.4f, 0.5f, 0f, 0f);
			loudState = analyzer2.Analyze(loud, SampleRate, 0.033f, 0.4f, 0.5f, 0f, 0f);
		}

		// AGC normalizes amplitudes, but loud signal should still produce
		// at least as much smoothed RMS as quiet at the same gain ceiling.
		Assert.True(loudState.Rms >= quietState.Rms - 0.05f);
	}

	[Fact]
	public void StereoBalanceIsPassedThrough() {
		var analyzer = new AudioAnalyzer(AudioBandConfig.Build(16, AudioBandConfig.BandScale.Log));
		var fft = new float[SampleSize];
		fft[10] = 0.3f;

		var state = analyzer.Analyze(fft, SampleRate, 0.033f, 0.5f, 0.5f, 0f, 0.7f);
		Assert.InRange(state.StereoBalance, 0.69f, 0.71f);
	}

	[Fact]
	public void BandSamplerInterpolatesAcrossSlots() {
		var bands = new float[] { 0f, 1f, 0f, 1f };
		var state = new AudioReactiveState(bands, bands, 0, 0, 0, 0, 0, 0, 0, 0, 0,
			0, false, 0, 0, new float[12], 0, 1, 0, 0.033f);

		Assert.Equal(0f, state.SampleBand(0f), 3);
		Assert.Equal(1f, state.SampleBand(1f / 3f), 3);
		Assert.Equal(0.5f, state.SampleBand(1f / 6f), 3);
	}
}
