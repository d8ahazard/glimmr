using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Glimmr.Models.FullSpectrum;
using Serilog;

namespace Glimmr.Services;

public class CalibrationService {
	private readonly RoomMapService _roomMapService;

	public CalibrationService(RoomMapService roomMapService) {
		_roomMapService = roomMapService;
	}

	/// <summary>
	/// Flash a single emitter for visual identification.
	/// Returns the emitter that was flashed, or null if not found.
	/// </summary>
	public RoomMapEmitter? FlashEmitter(string emitterId, RgbColor color) {
		var graph = _roomMapService.GetGraph();
		var emitter = graph.Emitters.FirstOrDefault(e => e.Id == emitterId);
		if (emitter == null) {
			Log.Warning("FlashEmitter: emitter '{Id}' not found.", emitterId);
			return null;
		}

		Log.Information("Flashing emitter '{Id}' with color R={R} G={G} B={B}.",
			emitterId, color.R, color.G, color.B);
		try {
			// Prefer physical-device flash by mapped device id; this drives the real output stack.
			if (!string.IsNullOrWhiteSpace(emitter.DeviceId)) {
				_ = ControlService.GetInstance().FlashDevice(emitter.DeviceId);
			} else if (emitter.Segments.Count > 0) {
				var led = Math.Max(0, emitter.Segments[0].StartIndex);
				_ = ControlService.GetInstance().FlashLed(led);
			}
		} catch (Exception e) {
			Log.Warning("FlashEmitter runtime flash failed: {Message}", e.Message);
		}

		return emitter;
	}

	/// <summary>
	/// Flash emitters in sequence for visual endpoint detection.
	/// Returns the list of emitter IDs that were chased.
	/// </summary>
	public async Task<List<string>> ChaseSequence(string[] emitterIds, CancellationToken ct = default) {
		var graph = _roomMapService.GetGraph();
		var chased = new List<string>();
		var white = new RgbColor { R = 255, G = 255, B = 255 };

		foreach (var id in emitterIds) {
			if (ct.IsCancellationRequested) break;

			var emitter = graph.Emitters.FirstOrDefault(e => e.Id == id);
			if (emitter == null) {
				Log.Warning("ChaseSequence: emitter '{Id}' not found, skipping.", id);
				continue;
			}

			Log.Debug("Chase: flashing emitter '{Id}'.", id);
			FlashEmitter(id, white);
			chased.Add(id);
			await Task.Delay(220, ct).ConfigureAwait(false);
		}

		return chased;
	}

	/// <summary>
	/// Generate a unique binary color pattern for camera-based emitter detection.
	/// Each emitter index gets a deterministic R/G/B pattern from its index bits.
	/// </summary>
	public RgbColor[] GenerateMarkerPattern(int emitterIndex) {
		const int bitCount = 8;
		var pattern = new RgbColor[bitCount];

		for (var bit = 0; bit < bitCount; bit++) {
			var on = ((emitterIndex >> bit) & 1) == 1;
			pattern[bit] = on
				? new RgbColor { R = 255, G = 255, B = 255 }
				: new RgbColor { R = 0, G = 0, B = 0 };
		}

		return pattern;
	}

	/// <summary>
	/// Given screen corners and observed LED positions, estimate 3D positions
	/// for each emitter using a simple projective mapping.
	/// </summary>
	public Dictionary<string, Vector3> SolvePositions(CalibrationInput input) {
		var result = new Dictionary<string, Vector3>();
		if (input.ScreenCorners.Length < 4) {
			Log.Warning("SolvePositions: need exactly 4 screen corners, got {Count}.", input.ScreenCorners.Length);
			return result;
		}

		var screenCenter = new Vector3 {
			X = input.ScreenCorners.Average(c => c.X),
			Y = input.ScreenCorners.Average(c => c.Y),
			Z = input.ScreenCorners.Average(c => c.Z)
		};

		var screenWidth = Distance(input.ScreenCorners[0], input.ScreenCorners[1]);
		var screenHeight = Distance(input.ScreenCorners[0], input.ScreenCorners[3]);
		if (screenWidth < 0.001) screenWidth = 1.0;
		if (screenHeight < 0.001) screenHeight = 1.0;

		foreach (var (emitterId, observed) in input.ObservedEmitters) {
			var relX = (observed.X - screenCenter.X) / screenWidth;
			var relY = (observed.Y - screenCenter.Y) / screenHeight;

			var estimated = new Vector3 {
				X = screenCenter.X + relX * screenWidth,
				Y = screenCenter.Y + relY * screenHeight,
				Z = observed.Z
			};

			if (input.ReferenceMarkers != null && input.ReferenceMarkers.TryGetValue(emitterId, out var reference)) {
				estimated = new Vector3 {
					X = (estimated.X + reference.X) / 2.0,
					Y = (estimated.Y + reference.Y) / 2.0,
					Z = (estimated.Z + reference.Z) / 2.0
				};
			}

			result[emitterId] = estimated;
		}

		ApplyToGraph(result);
		return result;
	}

	private void ApplyToGraph(Dictionary<string, Vector3> solved) {
		foreach (var (id, position) in solved) {
			_roomMapService.UpdateEmitter(id, position, null);
		}
		Log.Information("Applied {Count} solved positions to the emitter graph.", solved.Count);
	}

	private static double Distance(Vector3 a, Vector3 b) {
		var dx = a.X - b.X;
		var dy = a.Y - b.Y;
		var dz = a.Z - b.Z;
		return Math.Sqrt(dx * dx + dy * dy + dz * dz);
	}
}
