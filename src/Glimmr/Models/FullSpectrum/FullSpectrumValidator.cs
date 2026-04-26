using System.Collections.Generic;
using System.Linq;

namespace Glimmr.Models.FullSpectrum;

public static class FullSpectrumValidator {
	private static readonly string[] RequiredTargets = {
		"led", "adalight", "wled", "lifx", "yeelight", "openrgb", "nanoleaf", "hue", "huev2", "dreamscreen", "glimmr"
	};

	private static readonly string[] RequiredSources = {
		"ambient", "audio", "video", "udp", "dreamscreen", "audiovideo", "monitorr"
	};

	private static readonly string[] RequiredProtocols = {
		"ddp", "govee-lan", "hue-entertainment-v2", "nanoleaf-stream", "wled-udp", "lifx-lan", "hyperion", "artnet-sacn"
	};

	private static readonly string[] RequiredEcosystems = {
		"govee", "twinkly", "wiz", "shelly", "home-assistant", "zigbee2mqtt", "deconz", "rgbnet", "signalrgb",
		"pc-rgb", "ikea-dirigera", "tuya", "kasa-tapo", "elgato", "ledfx", "xlights-fpp", "prismatik"
	};

	public static ValidationReport Validate(FullSpectrumCatalog catalog) {
		var findings = new List<ValidationFinding>();
		CheckRequired(findings, "target", RequiredTargets, catalog.Targets.Select(t => t.Id));
		CheckRequired(findings, "source", RequiredSources, catalog.Sources.Select(s => s.Id));
		CheckRequired(findings, "protocol", RequiredProtocols, catalog.Protocols.Select(p => p.Id));
		CheckRequired(findings, "ecosystem", RequiredEcosystems, catalog.Ecosystems.Select(e => e.Id));

		foreach (var target in catalog.Targets) {
			CheckProvider(findings, "target", target);
		}

		foreach (var source in catalog.Sources) {
			CheckProvider(findings, "source", source);
		}

		foreach (var protocol in catalog.Protocols) {
			if (protocol.Priority <= 0) {
				Add(findings, "error", "protocol", protocol.Id, "Protocol priority must be positive.");
			}

			if (protocol.Transports.Count == 0 || protocol.UseCases.Count == 0) {
				Add(findings, "error", "protocol", protocol.Id, "Protocol must declare transports and use cases.");
			}
		}

		if (catalog.RoomMap.EmitterFields.Count == 0 || catalog.RoomMap.CalibrationModes.Count == 0 ||
		    catalog.RoomMap.SpatialEffects.Count == 0) {
			Add(findings, "error", "room-map", "room-map", "Room Map must declare emitter fields, calibration modes, and spatial effects.");
		}

		if (catalog.UserExperience.Count == 0) {
			Add(findings, "error", "ux", "ux", "At least one UX manifest is required.");
		}

		if (catalog.Validation.Count == 0) {
			Add(findings, "error", "validation", "validation", "At least one validation manifest is required.");
		}

		return new ValidationReport {
			Success = findings.All(f => f.Severity != "error"),
			Findings = findings
		};
	}

	private static void CheckRequired(List<ValidationFinding> findings, string area, IEnumerable<string> required,
		IEnumerable<string> actual) {
		var actualSet = actual.ToHashSet();
		foreach (var id in required) {
			if (!actualSet.Contains(id)) {
				Add(findings, "error", area, id, $"Required {area} manifest is missing.");
			}
		}
	}

	private static void CheckProvider(List<ValidationFinding> findings, string area, ProviderManifest provider) {
		if (string.IsNullOrWhiteSpace(provider.Id) || string.IsNullOrWhiteSpace(provider.Name) ||
		    string.IsNullOrWhiteSpace(provider.CodePath)) {
			Add(findings, "error", area, provider.Id, "Provider must declare id, name, and code path.");
		}

		if (provider.Protocols.Count == 0 || provider.Diagnostics.Count == 0 || provider.Backlog.Count == 0) {
			Add(findings, "error", area, provider.Id, "Provider must declare protocols, diagnostics, and backlog.");
		}

		if (provider.Kind == ProviderKind.ColorTarget && provider.RoomMapEmitters.Count == 0) {
			Add(findings, "error", area, provider.Id, "Target provider must declare Room Map emitter types.");
		}
	}

	private static void Add(List<ValidationFinding> findings, string severity, string area, string id, string message) {
		findings.Add(new ValidationFinding {
			Severity = severity,
			Area = area,
			Id = id,
			Message = message
		});
	}
}
