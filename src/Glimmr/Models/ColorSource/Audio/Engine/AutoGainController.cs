#region

using System;

#endregion

namespace Glimmr.Models.ColorSource.Audio.Engine;

/// <summary>
///     Peak-following AGC with separate attack/release.
///     Keeps quiet music visible without making loud peaks blow out the
///     visualizer. Mirrors the principle used by WLED's sound-reactive
///     mod, simplified for our cadence (~30 fps).
/// </summary>
public sealed class AutoGainController {
	private readonly float _attackPerSecond;
	private readonly float _maxGain;
	private readonly float _minGain;
	private readonly float _noiseFloor;
	private readonly float _releasePerSecond;
	private readonly float _target;
	private float _envelope;
	private float _gain = 1f;
	private float _gate;

	public AutoGainController(float target = 0.65f, float minGain = 1f, float maxGain = 32f,
		float attackPerSecond = 6f, float releasePerSecond = 0.25f, float noiseFloor = 0.0035f) {
		_target = target;
		_minGain = minGain;
		_maxGain = maxGain;
		_attackPerSecond = attackPerSecond;
		_releasePerSecond = releasePerSecond;
		_noiseFloor = noiseFloor;
	}

	public float Gain => _gain;
	public float Gate => _gate;

	public void Reset() {
		_gain = 1f;
		_envelope = 0f;
		_gate = 0f;
	}

	/// <summary>
	///     Update AGC state from the current peak magnitude (0..1) and apply
	///     the gain to the supplied band buffer in-place. <paramref name="userGain" />
	///     is a 0..1 user-facing multiplier baked into the maximum allowed gain
	///     so the slider behaves linearly.
	/// </summary>
	public void Process(float peak, float[] bands, float deltaSeconds, float userGain) {
		if (deltaSeconds <= 0f) {
			deltaSeconds = 0.033f;
		}

		userGain = Math.Clamp(userGain, 0f, 1f);
		var allowedMax = _minGain + (_maxGain - _minGain) * userGain;

		// Smoothed envelope of the peak.
		_envelope = Math.Max(_envelope * MathF.Exp(-deltaSeconds * 1.2f), peak);

		// Noise gate: opens when the envelope crosses the floor and closes
		// when it drops below half-floor (hysteresis prevents flicker).
		var openTarget = _envelope > _noiseFloor ? 1f : _envelope > _noiseFloor * 0.5f ? _gate : 0f;
		var gateLerp = openTarget > _gate ? deltaSeconds * 8f : deltaSeconds * 1.2f;
		_gate += (openTarget - _gate) * Math.Min(1f, gateLerp);

		// Compute the gain we'd want to hit our target.
		var desired = _envelope > 0.001f ? _target / _envelope : allowedMax;
		desired = Math.Clamp(desired, _minGain, allowedMax);

		// Asymmetric tracking: clamp down quickly, climb back up gently.
		var rate = desired < _gain ? _attackPerSecond : _releasePerSecond;
		_gain += (desired - _gain) * Math.Min(1f, rate * deltaSeconds);
		_gain = Math.Clamp(_gain, _minGain, allowedMax);

		var effectiveGain = _gain * _gate;
		for (var i = 0; i < bands.Length; i++) {
			var v = bands[i] * effectiveGain;
			if (v > 1f) {
				v = 1f;
			}

			bands[i] = v;
		}
	}
}
