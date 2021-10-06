#region

using System;
using Newtonsoft.Json;

#endregion

namespace Glimmr.Models.Util {
	[Serializable]
	public class CpuData {
		/// <summary>
		/// Load Average for the past minute.
		/// </summary>
		[JsonProperty] public float LoadAvg1 { get; set; }

		/// <summary>
		/// Load average for the past 15 minutes.
		/// </summary>
		[JsonProperty] public float LoadAvg15 { get; set; }

		/// <summary>
		/// Load average for the past 5 minutes.
		/// </summary>
		[JsonProperty] public float LoadAvg5 { get; set; }
		
		/// <summary>
		/// Average temperature.
		/// </summary>

		[JsonProperty] public float TempAvg { get; set; }
		
		/// <summary>
		/// Current temperature.
		/// </summary>

		[JsonProperty] public float TempCurrent { get; set; }
		
		/// <summary>
		/// Maximum detected temperature.
		/// </summary>

		[JsonProperty] public float TempMax { get; set; }
		
		/// <summary>
		/// Minimum detected temperature.
		/// </summary>

		[JsonProperty] public float TempMin { get; set; }
		
		/// <summary>
		/// System Uptime.
		/// </summary>

		[JsonProperty] public string? Uptime { get; set; }
		
		/// <summary>
		/// Current throttle state.
		/// </summary>

		[JsonProperty] public string[]? ThrottledState { get; set; }
	}
}