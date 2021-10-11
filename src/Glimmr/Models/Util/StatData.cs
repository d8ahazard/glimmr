#region

using System;
using System.Collections.Concurrent;
using Newtonsoft.Json;

#endregion

namespace Glimmr.Models.Util {
	[Serializable]
	public class StatData {
		/// <summary>
		/// Percentage of CPU Used
		/// </summary>
		[JsonProperty]
		public int CpuUsage { get; set; }

		/// <summary>
		/// Current CPU temperature (May not report on Windows)
		/// </summary>

		[JsonProperty]
		public float CpuTemp { get; set; }

		/// <summary>
		/// Current number of frames per second
		/// </summary>
		[JsonProperty]
		public ConcurrentDictionary<string, int> Fps { get; set; } = new();

		/// <summary>
		/// Total percentage of memory used in GB
		/// </summary>
		[JsonProperty]
		public float MemoryUsage { get; set; }

		/// <summary>
		/// Maximum detected temperature.
		/// </summary>

		[JsonProperty]
		public float TempMax { get; set; }

		/// <summary>
		/// Minimum detected temperature.
		/// </summary>

		[JsonProperty]
		public float TempMin { get; set; }

		/// <summary>
		/// System Uptime.
		/// </summary>

		[JsonProperty]
		public string Uptime {
			get {
				var t = TimeSpan.FromMilliseconds(Environment.TickCount);
				return $"{t.Days:D1}d, {t.Hours:D1}h:{t.Minutes:D2}m:{t.Seconds:D2}s";
			}
		}

		/// <summary>
		/// Current throttle state.
		/// </summary>

		[JsonProperty]
		public string[] ThrottledState { get; set; } = Array.Empty<string>();
	}
}