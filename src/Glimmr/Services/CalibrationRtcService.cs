using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using Glimmr.Models.FullSpectrum;
using Serilog;

namespace Glimmr.Services;

/// <summary>
/// Lightweight calibration ingest/session service. This is the backend coordination layer used
/// by the phone calibration UI. It accepts live camera frames and drives solve/finalize flows.
/// </summary>
public class CalibrationRtcService {
	private readonly ConcurrentDictionary<string, CalibrationSessionState> _sessions = new();
	private readonly CalibrationService _calibrationService;
	private readonly RoomMapService _roomMapService;

	public CalibrationRtcService(CalibrationService calibrationService, RoomMapService roomMapService) {
		_calibrationService = calibrationService;
		_roomMapService = roomMapService;
	}

	public CalibrationSessionState CreateSession(string sceneId) {
		PruneExpired();
		var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
		var session = new CalibrationSessionState {
			SessionId = Guid.NewGuid().ToString("N"),
			CreatedAtUnixMs = now,
			ExpiresAtUnixMs = now + (15 * 60 * 1000),
			Status = "created",
			SceneId = sceneId ?? "",
			FrameCount = 0
		};
		_sessions[session.SessionId] = session;
		var emitterIds = _roomMapService.GetGraph().Emitters.Select(e => e.Id).ToArray();
		if (emitterIds.Length > 0) {
			_ = _calibrationService.ChaseSequence(emitterIds);
		}
		Log.Information("Started calibration session {Id} with {Count} emitters.", session.SessionId, emitterIds.Length);
		return session;
	}

	public CalibrationSessionState? GetSession(string sessionId) {
		PruneExpired();
		if (!_sessions.TryGetValue(sessionId, out var state)) return null;
		return state;
	}

	public bool AddFrame(string sessionId, string dataUrl) {
		if (!_sessions.TryGetValue(sessionId, out var state)) return false;
		if (DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() > state.ExpiresAtUnixMs) {
			state.Status = "expired";
			return false;
		}

		// Keep memory bounded: we only track counts and recent frame signatures for diagnostics.
		state.FrameCount++;
		state.Status = "streaming";
		if (!string.IsNullOrWhiteSpace(dataUrl)) {
			state.LastFrameSignature = dataUrl.Length > 64 ? dataUrl.Substring(0, 64) : dataUrl;
		}

		if (state.FrameCount % 10 == 0) {
			Log.Debug("Calibration session {Id}: received {Count} frames.", sessionId, state.FrameCount);
		}

		return true;
	}

	public bool Complete(string sessionId) {
		if (!_sessions.TryGetValue(sessionId, out var state)) return false;
		var graph = _roomMapService.GetGraph();
		if (graph.Emitters.Count == 0) {
			state.Status = "solved";
			return true;
		}

		// Build a first-pass observed map around a virtual TV plane. This is intentionally simple
		// and keeps the pipeline wired end-to-end; later revisions can replace it with CV detection.
		var observed = new Dictionary<string, Vector3>();
		var i = 0;
		foreach (var emitter in graph.Emitters) {
			var angle = (Math.PI * 2.0 * i) / Math.Max(1, graph.Emitters.Count);
			observed[emitter.Id] = new Vector3 {
				X = Math.Cos(angle) * 1.7,
				Y = 1.1 + (Math.Sin(angle) * 0.5),
				Z = -2.1
			};
			i++;
		}

		var input = new CalibrationInput {
			ScreenCorners = new[] {
				new Vector3 { X = -1.2, Y = 2.0, Z = -2.2 },
				new Vector3 { X = 1.2, Y = 2.0, Z = -2.2 },
				new Vector3 { X = 1.2, Y = 0.6, Z = -2.2 },
				new Vector3 { X = -1.2, Y = 0.6, Z = -2.2 }
			},
			ObservedEmitters = observed
		};

		_calibrationService.SolvePositions(input);
		state.Status = "solved";
		return true;
	}

	private void PruneExpired() {
		var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
		foreach (var kvp in _sessions.Where(k => now > k.Value.ExpiresAtUnixMs).ToList()) {
			kvp.Value.Status = "expired";
			_sessions.TryRemove(kvp.Key, out _);
		}
	}
}

public sealed class CalibrationSessionState {
	public string SessionId { get; init; } = "";
	public long CreatedAtUnixMs { get; set; }
	public long ExpiresAtUnixMs { get; set; }
	public string Status { get; set; } = "created";
	public string SceneId { get; set; } = "";
	public int FrameCount { get; set; }
	public string LastFrameSignature { get; set; } = "";
}

public sealed class CalibrationSessionCreateRequest {
	public string SceneId { get; init; } = "";
}

public sealed class CalibrationFrameUploadRequest {
	public string DataUrl { get; init; } = "";
}
