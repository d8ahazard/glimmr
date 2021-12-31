#region

using System;
using System.Collections.Concurrent;
using Newtonsoft.Json;

#endregion

namespace Glimmr.Models;

[Serializable]
public class StatData {
	/// <summary>
	///     Current number of frames per second.
	/// </summary>
	[JsonProperty]
	public ConcurrentDictionary<string, int> Fps { get; set; } = new();

	/// <summary>
	///     Current CPU temperature (May not work with some AMD processors).
	/// </summary>

	[JsonProperty]
	public float CpuTemp { get; set; }

	/// <summary>
	///     Total percentage of memory used.
	/// </summary>
	[JsonProperty]
	public float MemoryUsage { get; set; }

	/// <summary>
	///     Maximum detected temperature.
	/// </summary>

	[JsonProperty]
	public float TempMax { get; set; }

	/// <summary>
	///     Minimum detected temperature.
	/// </summary>

	[JsonProperty]
	public float TempMin { get; set; }

	/// <summary>
	///     Percentage of CPU Used
	/// </summary>
	[JsonProperty]
	public int CpuUsage { get; set; }

	/// <summary>
	///     System Uptime.
	/// </summary>

	[JsonProperty]
	public string Uptime {
		get {
			var t = TimeSpan.FromMilliseconds(Environment.TickCount);
			return $"{t.Days:D1}d, {t.Hours:D1}h:{t.Minutes:D2}m:{t.Seconds:D2}s";
		}
	}

	/// <summary>
	///     Current throttle state.
	/// </summary>

	[JsonProperty]
	public string[] ThrottledState { get; set; } = Array.Empty<string>();
}