using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace Glimmr.Models.FullSpectrum;

public static class SpatialEffectsEngine {

	private static RgbColor _previousAverageColor = new();
	private static readonly Stopwatch SceneCutTimer = Stopwatch.StartNew();

	public static RgbColor[] ProcessFrame(SignalFrame frame, EmitterGraph graph, EffectMode mode) {
		return mode switch {
			EffectMode.ScreenEdgeProjection => ScreenEdgeProjection(frame, graph),
			EffectMode.WallWash => WallWash(frame, graph),
			EffectMode.RadialPulse => RadialPulse(frame, graph),
			EffectMode.LateralSweep => LateralSweep(frame, graph),
			EffectMode.SceneCut => SceneCut(frame, graph),
			EffectMode.AudioBeatWave => AudioBeatWave(frame, graph),
			_ => new RgbColor[graph.Emitters.Count]
		};
	}

	public static IReadOnlyList<EffectMode> AvailableModes => Enum.GetValues<EffectMode>();

	private static RgbColor[] ScreenEdgeProjection(SignalFrame frame, EmitterGraph graph) {
		var output = new RgbColor[graph.Emitters.Count];
		if (frame.Samples.Count == 0) return output;

		for (var i = 0; i < graph.Emitters.Count; i++) {
			var emitter = graph.Emitters[i];
			var nearest = FindNearestSample(emitter.Position, frame.Samples);
			output[i] = nearest?.Color ?? new RgbColor();
		}

		return output;
	}

	private static RgbColor[] WallWash(SignalFrame frame, EmitterGraph graph) {
		var output = new RgbColor[graph.Emitters.Count];
		if (frame.Samples.Count == 0) return output;

		var avgR = (int)frame.Samples.Average(s => s.Color.R);
		var avgG = (int)frame.Samples.Average(s => s.Color.G);
		var avgB = (int)frame.Samples.Average(s => s.Color.B);

		var color = new RgbColor {
			R = (byte)Math.Clamp(avgR, 0, 255),
			G = (byte)Math.Clamp(avgG, 0, 255),
			B = (byte)Math.Clamp(avgB, 0, 255)
		};

		Array.Fill(output, color);
		return output;
	}

	private static RgbColor[] RadialPulse(SignalFrame frame, EmitterGraph graph) {
		var output = new RgbColor[graph.Emitters.Count];
		if (frame.Samples.Count == 0) return output;

		var center = ComputeCentroid(frame.Samples);
		var maxDist = ComputeMaxDistance(graph.Emitters, center);
		if (maxDist < 0.001) maxDist = 1.0;

		var avgColor = AverageColor(frame.Samples);
		var phase = (frame.Sequence % 60) / 60.0;

		for (var i = 0; i < graph.Emitters.Count; i++) {
			var dist = Distance(graph.Emitters[i].Position, center);
			var normalized = dist / maxDist;
			var wave = Math.Max(0.0, 1.0 - Math.Abs(normalized - phase) * 3.0);
			output[i] = ScaleColor(avgColor, wave);
		}

		return output;
	}

	private static RgbColor[] LateralSweep(SignalFrame frame, EmitterGraph graph) {
		var output = new RgbColor[graph.Emitters.Count];
		if (frame.Samples.Count == 0 || graph.Emitters.Count == 0) return output;

		var minX = graph.Emitters.Min(e => e.Position.X);
		var maxX = graph.Emitters.Max(e => e.Position.X);
		var rangeX = maxX - minX;
		if (rangeX < 0.001) rangeX = 1.0;

		var sweepPos = (frame.Sequence % 120) / 120.0;
		var avgColor = AverageColor(frame.Samples);

		for (var i = 0; i < graph.Emitters.Count; i++) {
			var normalizedX = (graph.Emitters[i].Position.X - minX) / rangeX;
			var intensity = Math.Max(0.0, 1.0 - Math.Abs(normalizedX - sweepPos) * 4.0);
			output[i] = ScaleColor(avgColor, intensity);
		}

		return output;
	}

	private static RgbColor[] SceneCut(SignalFrame frame, EmitterGraph graph) {
		var output = new RgbColor[graph.Emitters.Count];
		if (frame.Samples.Count == 0) return output;

		var currentAvg = AverageColor(frame.Samples);
		var cutDetected = frame.Metadata.SceneCutConfidence > 0.5 ||
		                  ColorDistance(_previousAverageColor, currentAvg) > 100;

		_previousAverageColor = currentAvg;

		if (cutDetected) {
			SceneCutTimer.Restart();
		}

		var elapsed = SceneCutTimer.ElapsedMilliseconds;
		const long flashDurationMs = 300;
		if (elapsed < flashDurationMs) {
			var intensity = 1.0 - (elapsed / (double)flashDurationMs);
			var flash = ScaleColor(new RgbColor { R = 255, G = 255, B = 255 }, intensity);
			Array.Fill(output, flash);
		}

		return output;
	}

	private static RgbColor[] AudioBeatWave(SignalFrame frame, EmitterGraph graph) {
		var output = new RgbColor[graph.Emitters.Count];
		if (frame.Samples.Count == 0) return output;

		var beatConfidence = frame.Metadata.BeatConfidence;
		if (beatConfidence < 0.1) return output;

		var center = ComputeCentroid(frame.Samples);
		var maxDist = ComputeMaxDistance(graph.Emitters, center);
		if (maxDist < 0.001) maxDist = 1.0;

		var avgColor = AverageColor(frame.Samples);
		var phase = (frame.Sequence % 30) / 30.0;

		for (var i = 0; i < graph.Emitters.Count; i++) {
			var dist = Distance(graph.Emitters[i].Position, center);
			var normalized = dist / maxDist;
			var wave = Math.Max(0.0, 1.0 - Math.Abs(normalized - phase) * 2.5);
			var intensity = wave * beatConfidence;
			output[i] = ScaleColor(avgColor, intensity);
		}

		return output;
	}

	private static SignalSample? FindNearestSample(Vector3 position, IReadOnlyList<SignalSample> samples) {
		SignalSample? nearest = null;
		var minDist = double.MaxValue;
		foreach (var sample in samples) {
			var d = Distance(position, sample.Position);
			if (d >= minDist) continue;
			minDist = d;
			nearest = sample;
		}
		return nearest;
	}

	private static Vector3 ComputeCentroid(IReadOnlyList<SignalSample> samples) {
		if (samples.Count == 0) return new Vector3();
		return new Vector3 {
			X = samples.Average(s => s.Position.X),
			Y = samples.Average(s => s.Position.Y),
			Z = samples.Average(s => s.Position.Z)
		};
	}

	private static double ComputeMaxDistance(IReadOnlyList<RoomMapEmitter> emitters, Vector3 center) {
		var max = 0.0;
		foreach (var e in emitters) {
			var d = Distance(e.Position, center);
			if (d > max) max = d;
		}
		return max;
	}

	private static RgbColor AverageColor(IReadOnlyList<SignalSample> samples) {
		if (samples.Count == 0) return new RgbColor();
		return new RgbColor {
			R = (byte)Math.Clamp((int)samples.Average(s => s.Color.R), 0, 255),
			G = (byte)Math.Clamp((int)samples.Average(s => s.Color.G), 0, 255),
			B = (byte)Math.Clamp((int)samples.Average(s => s.Color.B), 0, 255)
		};
	}

	private static double Distance(Vector3 a, Vector3 b) {
		var dx = a.X - b.X;
		var dy = a.Y - b.Y;
		var dz = a.Z - b.Z;
		return Math.Sqrt(dx * dx + dy * dy + dz * dz);
	}

	private static double ColorDistance(RgbColor a, RgbColor b) {
		var dr = a.R - b.R;
		var dg = a.G - b.G;
		var db = a.B - b.B;
		return Math.Sqrt(dr * dr + dg * dg + db * db);
	}

	private static RgbColor ScaleColor(RgbColor color, double factor) {
		factor = Math.Clamp(factor, 0.0, 1.0);
		return new RgbColor {
			R = (byte)(color.R * factor),
			G = (byte)(color.G * factor),
			B = (byte)(color.B * factor)
		};
	}
}
