using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Glimmr.Models.FullSpectrum;
using Serilog;

namespace Glimmr.Services;

public class OutputSchedulerService {
	private readonly RoomMapService _roomMapService;
	private readonly ConcurrentDictionary<string, OutputSchedulerProfile> _profiles = new();
	private readonly ConcurrentDictionary<string, OutputTimingStats> _stats = new();
	private readonly ConcurrentDictionary<string, Stopwatch> _frameTimers = new();

	public OutputSchedulerService(RoomMapService roomMapService) {
		_roomMapService = roomMapService;
	}

	public void SetProfile(OutputSchedulerProfile profile) {
		_profiles[profile.ProviderId] = profile;
		_stats.TryAdd(profile.ProviderId, new OutputTimingStats { TargetId = profile.ProviderId });
		_frameTimers.TryAdd(profile.ProviderId, Stopwatch.StartNew());
		Log.Debug("Set output profile for provider '{Id}', target FPS={Fps}.",
			profile.ProviderId, profile.TargetFrameRate);
	}

	public void RemoveProfile(string providerId) {
		_profiles.TryRemove(providerId, out _);
		_stats.TryRemove(providerId, out _);
		_frameTimers.TryRemove(providerId, out _);
	}

	/// <summary>
	/// Process a frame through the spatial effects engine and produce per-target output.
	/// Returns a dictionary mapping provider ID to Color[] output, respecting timing profiles.
	/// </summary>
	public Dictionary<string, TargetFrameOutput> ProcessFrame(SignalFrame frame, EffectMode mode) {
		var graph = _roomMapService.GetGraph();
		var colors = SpatialEffectsEngine.ProcessFrame(frame, graph, mode);
		var results = new Dictionary<string, TargetFrameOutput>();

		var emittersByProvider = graph.Emitters
			.Select((e, i) => (Emitter: e, Index: i))
			.GroupBy(x => x.Emitter.ProviderId);

		foreach (var group in emittersByProvider) {
			var providerId = group.Key;
			if (!ShouldSendFrame(providerId)) {
				RecordDrop(providerId);
				continue;
			}

			var providerColors = group
				.Where(x => x.Index < colors.Length)
				.Select(x => colors[x.Index])
				.ToArray();

			results[providerId] = new TargetFrameOutput {
				ProviderId = providerId,
				Colors = providerColors,
				Timestamp = frame.TimestampUnixMs
			};

			RecordSend(providerId);
		}

		return results;
	}

	public OutputTimingStats? GetStats(string providerId) {
		_stats.TryGetValue(providerId, out var stats);
		return stats;
	}

	public IReadOnlyList<OutputTimingStats> GetAllStats() {
		return _stats.Values.ToList();
	}

	private bool ShouldSendFrame(string providerId) {
		if (!_profiles.TryGetValue(providerId, out var profile)) return true;
		if (!_frameTimers.TryGetValue(providerId, out var timer)) return true;

		var targetIntervalMs = profile.TargetFrameRate > 0
			? 1000.0 / profile.TargetFrameRate
			: 0;

		var elapsed = timer.Elapsed.TotalMilliseconds;

		if (elapsed < targetIntervalMs - profile.JitterBudgetMs) {
			return profile.AllowBackpressureDrop ? false : true;
		}

		return true;
	}

	private void RecordSend(string providerId) {
		if (!_stats.TryGetValue(providerId, out var stats)) return;
		if (!_frameTimers.TryGetValue(providerId, out var timer)) return;

		var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
		var sinceLastMs = timer.Elapsed.TotalMilliseconds;
		timer.Restart();

		stats.LastSendTimeMs = now;

		if (sinceLastMs > 0) {
			var instantFps = 1000.0 / sinceLastMs;
			stats.ActualFps = stats.ActualFps > 0
				? stats.ActualFps * 0.9 + instantFps * 0.1
				: instantFps;
		}

		if (_profiles.TryGetValue(providerId, out var profile)) {
			stats.AverageLatencyMs = stats.AverageLatencyMs * 0.9 +
			                         profile.LatencyCompensationMs * 0.1;
		}
	}

	private void RecordDrop(string providerId) {
		if (_stats.TryGetValue(providerId, out var stats)) {
			stats.DroppedFrames++;
		}
	}
}

public sealed class TargetFrameOutput {
	public string ProviderId { get; init; } = "";
	public RgbColor[] Colors { get; init; } = Array.Empty<RgbColor>();
	public long Timestamp { get; init; }
}
