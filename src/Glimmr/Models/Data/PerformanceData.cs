#region

using System;
using System.Collections.Generic;
using Newtonsoft.Json;

#endregion

namespace Glimmr.Models.Data;

[Serializable]
public class PerformanceData {
	[JsonProperty]
	public PipelinePerf Pipeline { get; set; } = new();

	[JsonProperty]
	public PreviewPerf Preview { get; set; } = new();

	[JsonProperty]
	public Dictionary<string, TargetSendPerf> Targets { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}

[Serializable]
public class PipelinePerf {
	[JsonProperty]
	public long FramesProcessed { get; set; }

	[JsonProperty]
	public long FramesDropped { get; set; }

	[JsonProperty]
	public double AvgUpdateMs { get; set; }

	[JsonProperty]
	public double AvgComputeMs { get; set; }

	[JsonProperty]
	public double AvgDispatchMs { get; set; }

	[JsonProperty]
	public double AvgPreviewMs { get; set; }

	[JsonProperty]
	public int MaxUpdateMs { get; set; }
}

[Serializable]
public class PreviewPerf {
	[JsonProperty]
	public long FramesSent { get; set; }

	[JsonProperty]
	public long BytesSent { get; set; }

	[JsonProperty]
	public double AvgEncodeAndSendMs { get; set; }

	[JsonProperty]
	public int MaxEncodeAndSendMs { get; set; }
}

[Serializable]
public class TargetSendPerf {
	[JsonProperty]
	public long Calls { get; set; }

	[JsonProperty]
	public long SkippedFrames { get; set; }

	[JsonProperty]
	public long Timeouts { get; set; }

	[JsonProperty]
	public long Errors { get; set; }

	[JsonProperty]
	public int InFlight { get; set; }

	[JsonProperty]
	public double AvgMs { get; set; }

	[JsonProperty]
	public int MaxMs { get; set; }
}
