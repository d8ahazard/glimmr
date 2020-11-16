using System;
using System.Reactive;
using Newtonsoft.Json;

namespace Glimmr.Models.Util {
	[Serializable]
	public class CpuData {
		[JsonProperty]
		public float TempCurrent { get; set; }
		[JsonProperty]
		public float TempAvg { get; set; }
		[JsonProperty]
		public float TempMin { get; set; }
		[JsonProperty]
		public float TempMax { get; set; }
		[JsonProperty]
		public string[] ThrottledState { get; set; }
		[JsonProperty]
		public float LoadAvg1 { get; set; }
		[JsonProperty]
		public float LoadAvg5 { get; set; }
		[JsonProperty]
		public float LoadAvg15 { get; set; }
		[JsonProperty]
		public string Uptime { get; set; }
		public CpuData() {
			
		}
	}
}