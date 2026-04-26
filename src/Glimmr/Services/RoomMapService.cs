using System;
using System.Collections.Generic;
using System.Linq;
using Glimmr.Models.FullSpectrum;
using Glimmr.Models.Util;
using Serilog;

namespace Glimmr.Services;

public class RoomMapService {
	private EmitterGraph _graph;
	private readonly object _lock = new();

	public RoomMapService() {
		_graph = LoadMap();
	}

	public EmitterGraph GetGraph() {
		lock (_lock) {
			return _graph;
		}
	}

	public EmitterGraph LoadMap() {
		try {
			var stored = DataUtil.GetObject<EmitterGraph>("EmitterGraph");
			if (stored is EmitterGraph g) {
				lock (_lock) {
					_graph = g;
				}
				Log.Information("Loaded EmitterGraph '{Name}' with {Count} emitters.", g.Name, g.Emitters.Count);
				return g;
			}
		} catch (Exception e) {
			Log.Warning("Failed to load EmitterGraph: {Message}", e.Message);
		}

		var empty = new EmitterGraph { Id = Guid.NewGuid().ToString(), Name = "Default" };
		lock (_lock) {
			_graph = empty;
		}
		return empty;
	}

	public void SaveMap() {
		lock (_lock) {
			try {
				DataUtil.SetObject<EmitterGraph>(_graph);
				Log.Debug("Saved EmitterGraph with {Count} emitters.", _graph.Emitters.Count);
			} catch (Exception e) {
				Log.Warning("Failed to save EmitterGraph: {Message}", e.Message);
			}
		}
	}

	public RoomMapEmitter AddEmitter(RoomMapEmitter emitter) {
		lock (_lock) {
			if (string.IsNullOrEmpty(emitter.Id)) {
				emitter.Id = Guid.NewGuid().ToString();
			}

			_graph.Emitters.Add(emitter);
			SaveMapInternal();
			return emitter;
		}
	}

	public bool RemoveEmitter(string emitterId) {
		lock (_lock) {
			var removed = _graph.Emitters.RemoveAll(e => e.Id == emitterId);
			if (removed > 0) {
				SaveMapInternal();
				return true;
			}
			return false;
		}
	}

	public RoomMapEmitter? UpdateEmitter(string emitterId, Vector3? position, Vector3? orientation) {
		lock (_lock) {
			var emitter = _graph.Emitters.FirstOrDefault(e => e.Id == emitterId);
			if (emitter == null) return null;

			if (position != null) emitter.Position = position;
			if (orientation != null) emitter.Orientation = orientation;

			SaveMapInternal();
			return emitter;
		}
	}

	public List<RoomMapEmitter> ImportFromProvider(string providerId) {
		lock (_lock) {
			var devices = DataUtil.GetDevices();
			var imported = new List<RoomMapEmitter>();

			foreach (var device in devices) {
				try {
					string tag = device.Tag;
					string id = device.Id;

					if (!string.IsNullOrEmpty(providerId) && tag?.ToLowerInvariant() != providerId.ToLowerInvariant()) {
						continue;
					}

					if (_graph.Emitters.Any(e => e.DeviceId == id && e.ProviderId == tag)) {
						continue;
					}

					var emitter = new RoomMapEmitter {
						Id = Guid.NewGuid().ToString(),
						ProviderId = tag ?? "Unknown",
						DeviceId = id ?? "",
						PhysicalType = tag ?? "Unknown",
						Position = new Vector3(),
						Orientation = new Vector3(),
						Normal = new Vector3 { Z = 1 }
					};

					_graph.Emitters.Add(emitter);
					imported.Add(emitter);
				} catch (Exception e) {
					Log.Warning("Failed to import device: {Message}", e.Message);
				}
			}

			if (imported.Count > 0) {
				SaveMapInternal();
				Log.Information("Imported {Count} emitters from provider '{Provider}'.", imported.Count, providerId);
			}

			return imported;
		}
	}

	public List<RoomMapEmitter> GetNearestEmitters(Vector3 position, int count) {
		lock (_lock) {
			return _graph.Emitters
				.OrderBy(e => Distance(e.Position, position))
				.Take(count)
				.ToList();
		}
	}

	private void SaveMapInternal() {
		try {
			DataUtil.SetObject<EmitterGraph>(_graph);
		} catch (Exception e) {
			Log.Warning("Failed to save EmitterGraph: {Message}", e.Message);
		}
	}

	private static double Distance(Vector3 a, Vector3 b) {
		var dx = a.X - b.X;
		var dy = a.Y - b.Y;
		var dz = a.Z - b.Z;
		return Math.Sqrt(dx * dx + dy * dy + dz * dz);
	}
}
