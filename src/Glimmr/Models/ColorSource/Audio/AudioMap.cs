#region

using System;
using System.Drawing;
using Glimmr.Models.ColorSource.Audio.Engine;
using Glimmr.Models.Data;
using Glimmr.Models.Helper;
using Glimmr.Models.Util;
using Serilog;

#endregion

namespace Glimmr.Models.ColorSource.Audio;

/// <summary>
///     Bridge between the BASS capture loop and the audio visualizer engine.
///     Owns the analyzer + the currently-active effect, and exposes the
///     last produced <see cref="AudioReactiveState" /> for clients (audio
///     + video, diagnostics) that want richer data than the 20-color
///     compatibility output.
/// </summary>
public class AudioMap {
	private readonly Color[] _output = new Color[PerimeterLayout.SlotCount];
	private readonly JsonLoader _loader = new("audioScenes");
	private AudioAnalyzer _analyzer;
	private AudioBandConfig _bandConfig;
	private float _defaultGain;
	private float _defaultNoiseGate;
	private IAudioEffect _effect;
	private int _loadedSceneId = int.MinValue;
	private string _loadedMode = string.Empty;
	private AudioScene _scene;

	public AudioMap() {
		_bandConfig = AudioBandConfig.Build(16, AudioBandConfig.BandScale.Log);
		_analyzer = new AudioAnalyzer(_bandConfig);
		_effect = AudioEffectRegistry.Resolve(AudioEffectRegistry.DefaultMode);
		_scene = new AudioScene { Mode = AudioEffectRegistry.DefaultMode };
		Refresh();
	}

	/// <summary>The most recent analyzer snapshot. Never null after first frame.</summary>
	public AudioReactiveState State { get; private set; } = AudioReactiveState.Empty;

	public IAudioEffect Effect => _effect;
	public AudioScene Scene => _scene;

	/// <summary>
	///     Re-load the scene + system data driven settings. Cheap; safe to
	///     call from the BASS callback.
	/// </summary>
	public void Refresh() {
		var sd = DataUtil.GetSystemData();
		AudioScene scene;
		try {
			scene = _loader.GetItem(sd.AudioScene);
		} catch (Exception e) {
			Log.Warning("Audio scene load failed: " + e.Message);
			scene = new AudioScene();
		}

		// Carry over legacy SystemData rotation knobs to legacy scenes that
		// have no modern overrides yet, so behaviour stays identical to the
		// old visualizer for users who never edit their scenes.
		if (scene.IsLegacy) {
			scene.RotationLower = sd.AudioRotationLower;
			scene.RotationUpper = sd.AudioRotationUpper;
			scene.RotationSpeed = sd.AudioRotationSpeed;
			scene.RotationThreshold = sd.AudioRotationTrigger;
		}

		_scene = scene;

		// Cache the SystemData-derived fallbacks here so Process() doesn't hit the
		// database on every audio frame.
		_defaultGain = sd.AudioGain;
		_defaultNoiseGate = sd.AudioCutoff > 0 ? Math.Min(1f, sd.AudioCutoff / 64f) : 0f;

		var desiredCount = NormalizeBandCount(scene.BandCount);
		var desiredScale = ParseScale(scene.BandScale);
		if (_bandConfig.BandCount != desiredCount || _bandConfig.Scale != desiredScale) {
			_bandConfig = AudioBandConfig.Build(desiredCount, desiredScale);
			_analyzer = new AudioAnalyzer(_bandConfig);
		} else {
			_analyzer.Reconfigure(_bandConfig);
		}

		_analyzer.SetSmoothing(
			scene.Attack > 0f ? scene.Attack : 0.55f,
			scene.Decay > 0f ? scene.Decay : 0.30f);

		var mode = scene.EffectiveMode;
		if (!string.Equals(mode, _loadedMode, StringComparison.OrdinalIgnoreCase) ||
		    scene.Id != _loadedSceneId) {
			_effect = AudioEffectRegistry.Resolve(mode);
			_effect.Reset();
			_loadedMode = mode;
			_loadedSceneId = scene.Id;
		}
	}

	/// <summary>
	///     Run the analyzer over a half-spectrum FFT magnitude buffer and
	///     return 20 perimeter colors ready for <see cref="Frame.FrameBuilder" />.
	/// </summary>
	public Color[] Process(float[] fft, int sampleRate, float deltaSeconds, float stereoBalance) {
		var sensitivity = _scene.Sensitivity > 0f ? _scene.Sensitivity : _defaultGain;
		var noiseGate = _scene.NoiseGate > 0f ? _scene.NoiseGate : _defaultNoiseGate;
		var beatSensitivity = _scene.BeatSensitivity > 0f ? _scene.BeatSensitivity : 0.6f;

		State = _analyzer.Analyze(fft, sampleRate, deltaSeconds, sensitivity, beatSensitivity,
			noiseGate, stereoBalance);

		try {
			_effect.Render(State, _scene, _output);
		} catch (Exception e) {
			Log.Warning($"Audio effect '{_loadedMode}' threw: {e.Message}");
			Array.Fill(_output, Color.FromArgb(0, 0, 0, 0));
		}

		return _output;
	}

	/// <summary>
	///     Force the output to black (used when the noise gate is closed for
	///     long enough that the engine's decay has fully settled).
	/// </summary>
	public Color[] Silence() {
		Array.Fill(_output, Color.FromArgb(0, 0, 0, 0));
		return _output;
	}

	private static int NormalizeBandCount(int requested) {
		if (requested < 8) {
			return 16;
		}

		if (requested > 64) {
			return 64;
		}

		return requested;
	}

	private static AudioBandConfig.BandScale ParseScale(string? scale) {
		if (string.IsNullOrWhiteSpace(scale)) {
			return AudioBandConfig.BandScale.Log;
		}

		return scale.ToLowerInvariant() switch {
			"mel" => AudioBandConfig.BandScale.Mel,
			"octave" => AudioBandConfig.BandScale.Octave,
			_ => AudioBandConfig.BandScale.Log
		};
	}
}
