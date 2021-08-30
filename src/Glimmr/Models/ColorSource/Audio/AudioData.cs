#region

using ManagedBass;
using Newtonsoft.Json;

#endregion

namespace Glimmr.Models.ColorSource.Audio {
	public class AudioData {
		[JsonProperty] public bool IsDefault { get; set; }

		[JsonProperty] public bool IsEnabled { get; set; }

		[JsonProperty] public bool IsInitialized { get; set; }

		[JsonProperty] public bool IsLoopback { get; set; }

		[JsonProperty] public DeviceType Type { get; set; }

		[JsonProperty] public string Driver { get; set; } = "";

		[JsonProperty] public string Id { get; set; } = "";

		[JsonProperty] public string Name { get; set; } = "";

		public void ParseDevice(DeviceInfo input) {
			Name = input.Name;
			Id = Name;
			Driver = input.Driver;
			IsDefault = input.IsDefault;
			IsEnabled = input.IsEnabled;
			IsInitialized = input.IsInitialized;
			IsLoopback = input.IsLoopback;
			Type = input.Type;
		}
	}
}