using System.Collections.Generic;

namespace Glimmr.Models.FullSpectrum;

public enum EffectMode {
	ScreenEdgeProjection,
	WallWash,
	RadialPulse,
	LateralSweep,
	SceneCut,
	AudioBeatWave
}

public sealed class EmitterGraph {
	public string Id { get; set; } = "";
	public string Name { get; set; } = "";
	public string Version { get; set; } = "1";
	public List<RoomMapEmitter> Emitters { get; set; } = new();
}

public sealed class CalibrationInput {
	public Vector3[] ScreenCorners { get; init; } = new Vector3[4];
	public Dictionary<string, Vector3> ObservedEmitters { get; init; } = new();
	public Dictionary<string, Vector3>? ReferenceMarkers { get; init; }
}

public sealed class OutputTimingStats {
	public string TargetId { get; set; } = "";
	public long LastSendTimeMs { get; set; }
	public double AverageLatencyMs { get; set; }
	public long DroppedFrames { get; set; }
	public double ActualFps { get; set; }
}

public sealed class RoomMapEmitter {
	public string Id { get; set; } = "";
	public string ProviderId { get; set; } = "";
	public string DeviceId { get; set; } = "";
	public string PhysicalType { get; set; } = "";
	public int AddressableCount { get; set; }
	public string ColorOrder { get; set; } = "RGB";
	public string Gamut { get; set; } = "Unknown";
	public RangeValue BrightnessRange { get; set; } = new() { Min = 0, Max = 100 };
	public double LatencyMs { get; set; }
	public double FrameRateCeiling { get; set; }
	public double PowerLimitWatts { get; set; }
	public Vector3 Position { get; set; } = new();
	public Vector3 Orientation { get; set; } = new();
	public Vector3 Normal { get; set; } = new();
	public EmitterDimensions Dimensions { get; set; } = new();
	public List<EmitterSegment> Segments { get; set; } = new();
	public double Confidence { get; set; }
}

public sealed class EmitterSegment {
	public string Id { get; init; } = "";
	public int StartIndex { get; init; }
	public int Count { get; init; }
	public Vector3 Start { get; init; } = new();
	public Vector3 End { get; init; } = new();
	public Vector3 Normal { get; init; } = new();
	public double Confidence { get; init; }
}

public sealed class SignalFrame {
	public string SourceId { get; init; } = "";
	public string SourceKind { get; init; } = "";
	public ulong Sequence { get; init; }
	public long TimestampUnixMs { get; init; }
	public string LayoutHash { get; init; } = "";
	public IReadOnlyList<SignalSample> Samples { get; init; } = new List<SignalSample>();
	public SignalFrameMetadata Metadata { get; init; } = new();
}

public sealed class SignalSample {
	public string Id { get; init; } = "";
	public Vector3 Position { get; init; } = new();
	public RgbColor Color { get; init; } = new();
	public double Intensity { get; init; }
	public double Confidence { get; init; }
}

public sealed class SignalFrameMetadata {
	public double CaptureLatencyMs { get; init; }
	public double ProcessingLatencyMs { get; init; }
	public double MotionX { get; init; }
	public double MotionY { get; init; }
	public double SceneCutConfidence { get; init; }
	public double BeatConfidence { get; init; }
}

public sealed class OutputSchedulerProfile {
	public string ProviderId { get; init; } = "";
	public RealtimeSafety RealtimeSafety { get; init; }
	public double TargetFrameRate { get; init; }
	public double LatencyCompensationMs { get; init; }
	public double JitterBudgetMs { get; init; }
	public bool AllowBackpressureDrop { get; init; }
}

public sealed class Vector3 {
	public double X { get; set; }
	public double Y { get; set; }
	public double Z { get; set; }
}

public sealed class EmitterDimensions {
	public double Width { get; init; }
	public double Height { get; init; }
	public double Depth { get; init; }
	public double Length { get; init; }
}

public sealed class RangeValue {
	public double Min { get; init; }
	public double Max { get; init; }
}

public sealed class RgbColor {
	public byte R { get; init; }
	public byte G { get; init; }
	public byte B { get; init; }
	public byte W { get; init; }
}
