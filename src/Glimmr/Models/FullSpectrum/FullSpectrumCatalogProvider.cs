using System.Collections.Generic;

namespace Glimmr.Models.FullSpectrum;

public static class FullSpectrumCatalogProvider {
	private static readonly FullSpectrumCatalog Catalog = new() {
		Targets = new List<ProviderManifest> {
			Target("led", "Pi WS281x LED", "src/Glimmr/Models/ColorTarget/Led", RealtimeSafety.RealtimeSafe,
				new[] { "Synthetic Raspberry Pi GPIO/PWM device discovery" },
				new[] { "Local hardware, no auth" },
				new[] { "rpi-ws281x Render()" },
				new[] { "ledmap", "offset", "multiplier", "reverse strip", "two-channel strip model" },
				new[] { "Pi strip", "PWM channel", "LED pixel" },
				new[] { "render cost", "channel brightness", "ABL scale", "configured LED count" },
				new[] { "Validate channel 0/1 behavior", "Expose max brightness as 0-255 internally and 0-100 in UI", "Record physical strip length and color order" }),
			Target("adalight", "Adalight", "src/Glimmr/Models/ColorTarget/Adalight", RealtimeSafety.RealtimeWithLimits,
				new[] { "Serial port probe", "Adalight handshake" },
				new[] { "Serial device, no auth" },
				new[] { "Adalight serial frames" },
				new[] { "ledmap", "offset", "multiplier", "gamma", "baud", "reverse strip" },
				new[] { "Serial strip", "LED pixel" },
				new[] { "port", "baud", "handshake", "write latency", "gamma table" },
				new[] { "Support non-COM port names", "Separate LED count from brightness in discovery", "Add reconnect/backoff" }),
			Target("wled", "WLED", "src/Glimmr/Models/ColorTarget/Wled", RealtimeSafety.RealtimeSafe,
				new[] { "mDNS _wled._tcp", "Manual IP fallback" },
				new[] { "No auth by default" },
				new[] { "WLED UDP WARLS/DRGB/DRGBW/DNRGB", "HTTP JSON state", "DDP future", "Art-Net/sACN future" },
				new[] { "ledmap", "sectorLedMap", "sectormap", "segments", "loop", "single color" },
				new[] { "Pixel", "Segment", "2D matrix cell" },
				new[] { "UDP FPS", "dropped frames", "segment merge", "live state", "DDP universe/offset" },
				new[] { "Add DDP output", "Import 2D matrix metadata", "Avoid sync HTTP", "Expose segment geometry" }),
			Target("lifx", "LIFX", "src/Glimmr/Models/ColorTarget/Lifx", RealtimeSafety.RealtimeWithLimits,
				new[] { "LIFX LAN discovery" },
				new[] { "LAN protocol, no auth" },
				new[] { "LIFX LAN SetColor", "Extended multizone", "Tile chain messages" },
				new[] { "sectormap", "beamMap", "tile layout", "gamma" },
				new[] { "Bulb", "Multizone strip zone", "Beam zone", "Tile pixel" },
				new[] { "zone count", "tile user position", "orientation", "LAN latency" },
				new[] { "Remove sync waits", "Prefer extended multizone messages", "Export tile chain orientation to Room Map" }),
			Target("yeelight", "Yeelight", "src/Glimmr/Models/ColorTarget/Yeelight", RealtimeSafety.RealtimeWithLimits,
				new[] { "Yeelight LAN discovery" },
				new[] { "LAN control must be enabled in Yeelight app" },
				new[] { "Yeelight JSON TCP", "Music mode socket", "Color flow scene mode" },
				new[] { "sectormap" },
				new[] { "Bulb", "Lamp zone when exposed" },
				new[] { "music mode state", "sector latency", "command failures" },
				new[] { "Harden music mode lifecycle", "Add reconnect handling", "Treat color flow as scene-only" }),
			Target("openrgb", "OpenRGB", "src/Glimmr/Models/ColorTarget/OpenRgb", RealtimeSafety.RealtimeWithLimits,
				new[] { "OpenRGB SDK server device enumeration" },
				new[] { "OpenRGB server connection" },
				new[] { "OpenRGB SDK TCP" },
				new[] { "ledmap", "sectormap", "strip modes", "color order" },
				new[] { "OpenRGB device LED", "Desktop RGB zone" },
				new[] { "server connection", "device index", "LED count", "direct mode" },
				new[] { "Add RGB.NET bridge", "Implement flash color", "Improve stable device identity" }),
			Target("nanoleaf", "Nanoleaf", "src/Glimmr/Models/ColorTarget/Nanoleaf", RealtimeSafety.RealtimeSafe,
				new[] { "mDNS _nanoleafapi._tcp" },
				new[] { "30-second app pairing window", "POST /api/v1/new token" },
				new[] { "Nanoleaf OpenAPI", "UDP external control", "Matter WiFi Essentials OpenAPI", "USB TLV/HID future" },
				new[] { "nanoleaf tile layout", "panel target sectors", "stream-addressable strip zones future" },
				new[] { "Panel", "Shape", "Essentials strip zone", "USB light-strip zone" },
				new[] { "pairing state", "layout version", "effect mode", "stream restart health" },
				new[] { "Remove blocking discovery", "Avoid runtime device construction during discovery", "Add Matter Essentials and USB light-strip support" }),
			Target("hue", "Philips Hue v1", "src/Glimmr/Models/ColorTarget/Hue", RealtimeSafety.SceneOnly,
				new[] { "Legacy discovery disabled" },
				new[] { "Bridge link button" },
				new[] { "Q42 Hue API", "Hue Entertainment v1" },
				new[] { "hue entertainment group light sectors" },
				new[] { "Entertainment light" },
				new[] { "bridge auth", "legacy group state" },
				new[] { "Move users to HueV2", "Document legacy-only status", "Add Swagger subtype only if retained" }),
			Target("huev2", "Philips Hue v2", "src/Glimmr/Models/ColorTarget/HueV2", RealtimeSafety.RealtimeSafe,
				new[] { "mDNS/HTTPS bridge discovery", "discovery.meethue.com fallback" },
				new[] { "Bridge link button", "app key", "streaming key" },
				new[] { "Hue API v2", "Entertainment API v2", "local event stream" },
				new[] { "entertainment groups", "channels", "gradient segments" },
				new[] { "Entertainment channel", "Gradient segment", "Bulb" },
				new[] { "stream connection", "channel map", "gradient segment count", "event stream health" },
				new[] { "Prioritize gradient segment control", "Avoid REST for realtime updates", "Remove sync waits" }),
			Target("dreamscreen", "DreamScreen", "src/Glimmr/Models/ColorTarget/DreamScreen", RealtimeSafety.SceneOnly,
				new[] { "DreamScreenNet discovery" },
				new[] { "No auth" },
				new[] { "DreamScreen UDP/library protocol" },
				new[] { "fixed 12-sector output" },
				new[] { "DreamScreen side sector" },
				new[] { "device group", "mode", "streaming flag" },
				new[] { "Label as legacy", "Add Hyperion/HyperHDR migration", "Clarify source/target behavior" }),
			Target("glimmr", "Glimmr Peer", "src/Glimmr/Models/ColorTarget/Glimmr", RealtimeSafety.RealtimeSafe,
				new[] { "mDNS _glimmr._tcp" },
				new[] { "LAN peer, no auth yet" },
				new[] { "Glimmr HTTP mode", "UDP ColorPacket" },
				new[] { "remote LED counts", "horizontal mirror" },
				new[] { "Remote Glimmr emitter graph" },
				new[] { "peer version", "remote counts", "endpoint casing", "packet drops" },
				new[] { "Version peer protocol", "Normalize endpoint casing", "Add capability exchange and shared Room Map" })
		},
		Sources = new List<ProviderManifest> {
			Source("ambient", "Ambient Scenes", "src/Glimmr/Models/ColorSource/Ambient", RealtimeSafety.RealtimeSafe,
				new[] { "Local scene JSON", "Solid color" }, new[] { "FrameBuilder synthetic frame", "ColorMatrix" },
				new[] { "sector animation", "matrix", "future spatial fields" },
				new[] { "scene validation", "easing mode", "matrix dimensions", "timer drift" },
				new[] { "Validate JSON", "Use PeriodicTimer", "Add deterministic replay tests" }),
			Source("audio", "Audio", "src/Glimmr/Models/ColorSource/Audio", RealtimeSafety.RealtimeSafe,
				new[] { "ManagedBass recording device", "FFT bins" }, new[] { "Bass recording callback", "FrameBuilder synthetic frame" },
				new[] { "20 logical audio sectors", "frequency bands", "future beat/onset spatial pulses" },
				new[] { "record device", "sample rate", "FFT sizing", "level gate", "restart state" },
				new[] { "Cross-platform frequency detection", "Beat/onset detection", "Replayable audio fixtures" }),
			Source("video", "Video", "src/Glimmr/Models/ColorSource/Video", RealtimeSafety.RealtimeSafe,
				new[] { "USB/HDMI capture", "PiCam", "screen capture", "macOS screenshot" }, new[] { "Emgu CV Mat", "FrameSplitter crop pipeline" },
				new[] { "LED rectangles", "sector grid", "preview overlays" },
				new[] { "backend FPS", "capture latency", "hot-plug state", "preview cost" },
				new[] { "Add throttling", "Hot-plug/index refresh", "GPU/offscreen processing option" }),
			Source("udp", "UDP", "src/Glimmr/Models/ColorSource/Udp", RealtimeSafety.RealtimeSafe,
				new[] { "UDP 21324", "mDNS _glimmr._tcp advertisement" }, new[] { "WARLS", "DRGB", "DRGBW", "DNRGB", "DDP future" },
				new[] { "GlimmrData dimensions", "layout negotiation future" },
				new[] { "duration timeout", "source active state", "packet mode", "layout count" },
				new[] { "Add DDP receive", "Formalize packet specs", "Add stale/reorder handling" }),
			Source("dreamscreen", "DreamScreen Source", "src/Glimmr/Models/ColorSource/DreamScreen", RealtimeSafety.SceneOnly,
				new[] { "DreamScreen subscription" }, new[] { "DreamScreenNet colors and mode messages" },
				new[] { "fixed 12-sector incoming source" },
				new[] { "mode transitions", "subscription health", "source priority" },
				new[] { "Clarify Ambient/Off interactions", "Expose as named replayable source" }),
			Source("audiovideo", "Audio + Video", "src/Glimmr/Models/ColorSource/AudioVideo", RealtimeSafety.RealtimeSafe,
				new[] { "Composite AudioStream and VideoStream" }, new[] { "video colors modulated by audio brightness" },
				new[] { "matching LED and sector arrays", "future generic signal composition" },
				new[] { "child stream health", "array length mismatches", "merge preview" },
				new[] { "Generic signal composer", "Avoid spin loops", "Add modulation fixtures" }),
			Source("monitorr", "Monitorr Realtime", "src/Glimmr/Models/ColorSource/Monitorr", RealtimeSafety.RealtimeSafe,
				new[] { "LAN WebSocket binary frames" }, new[] { "MRTL v1", "layout hash", "edge color payload" },
				new[] { "packet-provided side counts", "TV LED Sync ingest" },
				new[] { "sequence gaps", "layout hash changes", "jitter", "stale packets" },
				new[] { "Promote to first-class provider", "Version MRTL protocol", "Add latency/jitter metrics" })
		},
		Protocols = new List<ProtocolManifest> {
			Protocol("ddp", "DDP", 1, RealtimeSafety.RealtimeSafe, new[] { "UDP 4048", "WebSocket future" }, new[] { "WLED realtime", "FPP/xLights", "large pixel arrays" }, new[] { "WLED", "FPP", "xLights", "Custom LEDs" }, new[] { "Chunk to 480 RGB pixels per packet", "Track packet sequence and offset", "Prefer for high-rate LED strips" }),
			Protocol("govee-lan", "Govee LAN", 2, RealtimeSafety.RealtimeWithLimits, new[] { "UDP multicast 239.255.255.250:4001", "UDP client 4002", "UDP control 4003" }, new[] { "Govee local control", "Segment control where supported" }, new[] { "Govee" }, new[] { "Require LAN switch enabled", "Use cloud inventory for capabilities", "Gate DreamView/Razer subprotocol by model" }),
			Protocol("hue-entertainment-v2", "Hue Entertainment API v2", 3, RealtimeSafety.RealtimeSafe, new[] { "Local encrypted streaming" }, new[] { "Hue gradients", "Entertainment rooms" }, new[] { "Philips Hue" }, new[] { "Use v2 channels for gradient segments", "Do not use REST for continuous updates" }),
			Protocol("nanoleaf-stream", "Nanoleaf Stream Control", 4, RealtimeSafety.RealtimeSafe, new[] { "UDP external control", "OpenAPI stream control" }, new[] { "Panels", "Matter Essentials strips" }, new[] { "Nanoleaf" }, new[] { "Keep external control alive", "Import panel geometry" }),
			Protocol("artnet-sacn", "Art-Net and sACN/E1.31", 5, RealtimeSafety.RealtimeSafe, new[] { "UDP Art-Net", "UDP E1.31" }, new[] { "Stage controllers", "Architectural lighting", "DMX universes" }, new[] { "WLED", "DMX controllers", "FPP" }, new[] { "170 RGB LEDs per universe", "Add universe planning UI", "Support RGBW fixtures" }),
			Protocol("wled-udp", "WLED UDP Realtime", 6, RealtimeSafety.RealtimeSafe, new[] { "UDP 21324" }, new[] { "Current WLED output", "FlowLED/Glimmr packet compatibility" }, new[] { "WLED", "Glimmr peers" }, new[] { "Keep WARLS/DRGB/DRGBW/DNRGB compatibility", "Prefer DDP for larger pixel counts once available" }),
			Protocol("lifx-lan", "LIFX LAN", 7, RealtimeSafety.RealtimeWithLimits, new[] { "UDP LAN protocol" }, new[] { "Bulbs", "multizone strips", "beams", "tiles" }, new[] { "LIFX" }, new[] { "Use extended multizone packets", "Import tile user positions and orientation", "Profile latency by model" }),
			Protocol("hyperion", "Hyperion/HyperHDR APIs", 8, RealtimeSafety.BridgeOnly, new[] { "JSON-RPC TCP 19444", "WebSocket/HTTP 8090/8092", "protobuf/FlatBuffers" }, new[] { "Migration", "Interop", "Priority-based ambient frames" }, new[] { "Hyperion", "HyperHDR" }, new[] { "Import instances/layouts", "Respect priority model", "Bridge, do not duplicate running owners" })
		},
		Ecosystems = new List<EcosystemManifest> {
			Ecosystem("govee", "Govee", 1, RealtimeSafety.RealtimeWithLimits, new[] { "Cloud inventory", "LAN control", "DreamView research" }, new[] { "segment_color_setting", "dynamic_scene", "gradientToggle", "dreamViewToggle" }, new[] { "Cloud rate limits", "LAN support varies by SKU" }),
			Ecosystem("twinkly", "Twinkly", 2, RealtimeSafety.RealtimeWithLimits, new[] { "Local HTTP", "challenge-response auth", "movie/effect upload" }, new[] { "pixel maps", "brightness", "color", "effects" }, new[] { "Private API", "token expiry" }),
			Ecosystem("home-assistant", "Home Assistant", 3, RealtimeSafety.BridgeOnly, new[] { "WebSocket API", "service calls", "state subscriptions" }, new[] { "broad entity import", "fallback control", "automation context" }, new[] { "Not a high-rate realtime path for most lights" }),
			Ecosystem("zigbee2mqtt", "Zigbee2MQTT", 4, RealtimeSafety.BridgeOnly, new[] { "MQTT topics", "device exposes" }, new[] { "xy", "hex/RGB", "brightness", "transition", "availability" }, new[] { "Device-specific exposes", "Separate color/on command ordering" }),
			Ecosystem("rgbnet", "RGB.NET", 5, RealtimeSafety.RealtimeWithLimits, new[] { "Desktop .NET SDK" }, new[] { "Corsair", "Razer", "Logitech", "ASUS", "MSI", "OpenRGB", "DMX", "WLED", "WS281X" }, new[] { "Desktop-only providers", "Vendor SDK install requirements" }),
			Ecosystem("wiz", "WiZ", 6, RealtimeSafety.SceneOnly, new[] { "Local UDP 38899" }, new[] { "setPilot", "getDevInfo", "dual-zone support" }, new[] { "Local communication must be enabled", "Firmware variations" }),
			Ecosystem("shelly", "Shelly RGBW", 7, RealtimeSafety.SceneOnly, new[] { "Local Gen2 RPC" }, new[] { "RGB/RGBW profiles", "power telemetry", "independent channels" }, new[] { "Not intended for high-rate video sync" }),
			Ecosystem("pc-rgb", "PC RGB SDKs", 8, RealtimeSafety.RealtimeWithLimits, new[] { "Razer Chroma", "Corsair iCUE", "Logitech LIGHTSYNC", "SteelSeries GameSense", "ASUS Aura", "SignalRGB" }, new[] { "keyboards", "mice", "mats", "fans", "motherboards" }, new[] { "Windows/desktop dependence", "heartbeat/session requirements" }),
			Ecosystem("signalrgb", "SignalRGB", 9, RealtimeSafety.BridgeOnly, new[] { "Local REST API 127.0.0.1:16038", "plugin path future" }, new[] { "canvas/effect control", "desktop RGB inventory" }, new[] { "Deep frame injection likely needs plugin work" }),
			Ecosystem("deconz", "deCONZ/Phoscon", 10, RealtimeSafety.SceneOnly, new[] { "REST API", "WebSocket events" }, new[] { "xy", "bri", "ct", "Zigbee light state" }, new[] { "Bridge latency", "Hue-compatible but not Hue Entertainment" }),
			Ecosystem("ikea-dirigera", "IKEA Dirigera", 11, RealtimeSafety.SceneOnly, new[] { "Local REST", "WebSocket events" }, new[] { "hub lights", "Matter/Thread inventory", "state import" }, new[] { "Unofficial API surface", "Not realtime" }),
			Ecosystem("tuya", "Tuya", 12, RealtimeSafety.CloudAssisted, new[] { "Cloud IoT Core", "standard instruction set" }, new[] { "colour_data_v2", "work_mode", "brightness", "temperature" }, new[] { "Cloud latency and rate limits", "Local APIs vary by device" }),
			Ecosystem("kasa-tapo", "TP-Link Kasa/Tapo", 13, RealtimeSafety.SceneOnly, new[] { "Local KLAP/Secure Passthrough", "community clients" }, new[] { "HSV", "brightness", "color temperature", "strips and bulbs" }, new[] { "Auth protocol variation", "Model-specific support" }),
			Ecosystem("elgato", "Elgato Key Light", 14, RealtimeSafety.SceneOnly, new[] { "Local HTTP port 9123" }, new[] { "on/off", "brightness", "color temperature" }, new[] { "White-temperature fixtures, not color realtime emitters" }),
			Ecosystem("ledfx", "LedFx", 15, RealtimeSafety.BridgeOnly, new[] { "REST API", "WebSocket API" }, new[] { "audio-reactive effect state", "WLED device/effect interop" }, new[] { "Interop/import first; Glimmr remains the spatial orchestrator" }),
			Ecosystem("xlights-fpp", "xLights/FPP", 16, RealtimeSafety.BridgeOnly, new[] { "DDP", "E1.31", "Art-Net", "FSEQ/FPP APIs" }, new[] { "layout import", "show-player coexistence", "holiday controller support" }, new[] { "Show-player ownership and scheduling conflicts" }),
			Ecosystem("prismatik", "Prismatik/Lightpack", 17, RealtimeSafety.SceneOnly, new[] { "Legacy socket API 3636" }, new[] { "LED count", "setcolor", "profiles", "migration" }, new[] { "Legacy ecosystem; implement only as easy migration path" })
		},
		RoomMap = new RoomMapManifest {
			EmitterFields = new[] { "id", "provider", "physical type", "addressable count", "color order", "gamut", "brightness range", "latency", "frame-rate ceiling", "power limit", "dimensions", "position", "orientation", "normal vector", "segment geometry", "confidence" },
			GeometryImporters = new[] { "Nanoleaf tile layouts", "LIFX tile user positions/orientation", "Hue Entertainment positions", "WLED segments/matrices", "Twinkly maps", "OpenRGB/RGB.NET LED coordinates", "manual bulbs/strips" },
			CalibrationModes = new[] { "flash whole device", "flash segment", "chase endpoints", "binary marker patterns", "QR/ArUco helper screens", "camera-visible color markers" },
			SolverInputs = new[] { "phone photos/video", "TV rectangle", "colored LED marker frames", "ArUco/ChArUco markers", "manual corrections" },
			SpatialEffects = new[] { "screen-edge projection", "wall wash", "explosion radial pulse", "pan sweep", "scene-cut global pulse", "bright-object bloom", "audio beat wave" }
		},
		UserExperience = new List<UserExperienceManifest> {
			new() { Id = "onboarding", Name = "Immersive Onboarding", Outcomes = new[] { "find devices", "link accounts/bridges", "verify realtime capability", "flash identify", "build Room Map", "preview output", "save backup" }, PreviewSurfaces = new[] { "provider checklist", "pairing progress", "latency badge", "Room Map confidence" } },
			new() { Id = "provider-dashboard", Name = "Provider Dashboard", Outcomes = new[] { "Installed", "Available", "Experimental", "Cloud-only", "Realtime-capable", "Room Map-ready" }, PreviewSurfaces = new[] { "capability cards", "diagnostic cards", "integration health" } },
			new() { Id = "source-target-previews", Name = "Realtime Previews", Outcomes = new[] { "source inspection", "target inspection", "room-field inspection" }, PreviewSurfaces = new[] { "input frame", "edge samples", "audio bands", "packet decode", "last target packet", "dropped frames", "gamut clipping" } }
		},
		Validation = new List<ValidationManifest> {
			new() { Id = "target-contracts", Name = "Target Provider Contracts", RequiredChecks = new[] { "discovery test", "auth/link test when applicable", "packet generation test", "capability snapshot", "hardware smoke checklist" } },
			new() { Id = "source-contracts", Name = "Source Provider Contracts", RequiredChecks = new[] { "lifecycle test", "replay test", "preview metadata test", "cancellation test", "latency budget check" } },
			new() { Id = "ecosystem-contracts", Name = "Ecosystem Classification", RequiredChecks = new[] { "realtime-safe/local/cloud/bridge classification", "secret redaction", "rate-limit behavior", "fallback behavior" } },
			new() { Id = "room-map-contracts", Name = "Room Map Validation", RequiredChecks = new[] { "user-correctable inference", "export/import", "confidence score", "local photo processing by default" } }
		}
	};

	public static FullSpectrumCatalog GetCatalog() {
		return Catalog;
	}

	public static ProviderManifest? GetTarget(string id) {
		foreach (var target in Catalog.Targets) {
			if (target.Id == id) {
				return target;
			}
		}

		return null;
	}

	public static ProviderManifest? GetSource(string id) {
		foreach (var source in Catalog.Sources) {
			if (source.Id == id) {
				return source;
			}
		}

		return null;
	}

	private static ProviderManifest Target(string id, string name, string path, RealtimeSafety realtimeSafety,
		IReadOnlyList<string> discovery, IReadOnlyList<string> linking, IReadOnlyList<string> protocols,
		IReadOnlyList<string> mapping, IReadOnlyList<string> emitters, IReadOnlyList<string> diagnostics,
		IReadOnlyList<string> backlog) {
		return new ProviderManifest {
			Id = id,
			Name = name,
			Kind = ProviderKind.ColorTarget,
			CodePath = path,
			RealtimeSafety = realtimeSafety,
			Discovery = discovery,
			Linking = linking,
			Protocols = protocols,
			Mapping = mapping,
			RoomMapEmitters = emitters,
			Diagnostics = diagnostics,
			Backlog = backlog
		};
	}

	private static ProviderManifest Source(string id, string name, string path, RealtimeSafety realtimeSafety,
		IReadOnlyList<string> ingest, IReadOnlyList<string> protocols, IReadOnlyList<string> mapping,
		IReadOnlyList<string> diagnostics, IReadOnlyList<string> backlog) {
		return new ProviderManifest {
			Id = id,
			Name = name,
			Kind = ProviderKind.ColorSource,
			CodePath = path,
			RealtimeSafety = realtimeSafety,
			Discovery = ingest,
			Protocols = protocols,
			Mapping = mapping,
			Diagnostics = diagnostics,
			Backlog = backlog
		};
	}

	private static ProtocolManifest Protocol(string id, string name, int priority, RealtimeSafety realtimeSafety,
		IReadOnlyList<string> transports, IReadOnlyList<string> useCases, IReadOnlyList<string> providers,
		IReadOnlyList<string> notes) {
		return new ProtocolManifest {
			Id = id,
			Name = name,
			Priority = priority,
			RealtimeSafety = realtimeSafety,
			Transports = transports,
			UseCases = useCases,
			Providers = providers,
			ImplementationNotes = notes
		};
	}

	private static EcosystemManifest Ecosystem(string id, string name, int priority, RealtimeSafety realtimeSafety,
		IReadOnlyList<string> modes, IReadOnlyList<string> capabilities, IReadOnlyList<string> constraints) {
		return new EcosystemManifest {
			Id = id,
			Name = name,
			Priority = priority,
			RealtimeSafety = realtimeSafety,
			IntegrationModes = modes,
			Capabilities = capabilities,
			Constraints = constraints
		};
	}
}
