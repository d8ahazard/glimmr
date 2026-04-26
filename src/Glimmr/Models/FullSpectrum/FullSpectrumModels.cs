using System.Collections.Generic;

namespace Glimmr.Models.FullSpectrum;

public enum ProviderKind {
	ColorTarget,
	ColorSource,
	Protocol,
	Ecosystem,
	RoomMap,
	UserExperience,
	Validation
}

public enum RealtimeSafety {
	RealtimeSafe,
	RealtimeWithLimits,
	SceneOnly,
	BridgeOnly,
	CloudAssisted,
	Experimental
}

public sealed class FullSpectrumCatalog {
	public IReadOnlyList<ProviderManifest> Targets { get; init; } = new List<ProviderManifest>();
	public IReadOnlyList<ProviderManifest> Sources { get; init; } = new List<ProviderManifest>();
	public IReadOnlyList<ProtocolManifest> Protocols { get; init; } = new List<ProtocolManifest>();
	public IReadOnlyList<EcosystemManifest> Ecosystems { get; init; } = new List<EcosystemManifest>();
	public RoomMapManifest RoomMap { get; init; } = new();
	public IReadOnlyList<UserExperienceManifest> UserExperience { get; init; } = new List<UserExperienceManifest>();
	public IReadOnlyList<ValidationManifest> Validation { get; init; } = new List<ValidationManifest>();
}

public sealed class ProviderManifest {
	public string Id { get; init; } = "";
	public string Name { get; init; } = "";
	public ProviderKind Kind { get; init; }
	public string CodePath { get; init; } = "";
	public RealtimeSafety RealtimeSafety { get; init; }
	public IReadOnlyList<string> Discovery { get; init; } = new List<string>();
	public IReadOnlyList<string> Linking { get; init; } = new List<string>();
	public IReadOnlyList<string> Protocols { get; init; } = new List<string>();
	public IReadOnlyList<string> Mapping { get; init; } = new List<string>();
	public IReadOnlyList<string> RoomMapEmitters { get; init; } = new List<string>();
	public IReadOnlyList<string> Diagnostics { get; init; } = new List<string>();
	public IReadOnlyList<string> Backlog { get; init; } = new List<string>();
}

public sealed class ProtocolManifest {
	public string Id { get; init; } = "";
	public string Name { get; init; } = "";
	public int Priority { get; init; }
	public RealtimeSafety RealtimeSafety { get; init; }
	public IReadOnlyList<string> Transports { get; init; } = new List<string>();
	public IReadOnlyList<string> UseCases { get; init; } = new List<string>();
	public IReadOnlyList<string> Providers { get; init; } = new List<string>();
	public IReadOnlyList<string> ImplementationNotes { get; init; } = new List<string>();
}

public sealed class EcosystemManifest {
	public string Id { get; init; } = "";
	public string Name { get; init; } = "";
	public int Priority { get; init; }
	public RealtimeSafety RealtimeSafety { get; init; }
	public IReadOnlyList<string> IntegrationModes { get; init; } = new List<string>();
	public IReadOnlyList<string> Capabilities { get; init; } = new List<string>();
	public IReadOnlyList<string> Constraints { get; init; } = new List<string>();
}

public sealed class RoomMapManifest {
	public IReadOnlyList<string> EmitterFields { get; init; } = new List<string>();
	public IReadOnlyList<string> GeometryImporters { get; init; } = new List<string>();
	public IReadOnlyList<string> CalibrationModes { get; init; } = new List<string>();
	public IReadOnlyList<string> SolverInputs { get; init; } = new List<string>();
	public IReadOnlyList<string> SpatialEffects { get; init; } = new List<string>();
}

public sealed class UserExperienceManifest {
	public string Id { get; init; } = "";
	public string Name { get; init; } = "";
	public IReadOnlyList<string> Outcomes { get; init; } = new List<string>();
	public IReadOnlyList<string> PreviewSurfaces { get; init; } = new List<string>();
}

public sealed class ValidationManifest {
	public string Id { get; init; } = "";
	public string Name { get; init; } = "";
	public IReadOnlyList<string> RequiredChecks { get; init; } = new List<string>();
}

public sealed class ValidationReport {
	public bool Success { get; init; }
	public IReadOnlyList<ValidationFinding> Findings { get; init; } = new List<ValidationFinding>();
}

public sealed class ValidationFinding {
	public string Severity { get; init; } = "";
	public string Area { get; init; } = "";
	public string Id { get; init; } = "";
	public string Message { get; init; } = "";
}
