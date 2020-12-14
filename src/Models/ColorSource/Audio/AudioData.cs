using ManagedBass;

namespace Glimmr.Models.ColorSource.Audio {
	public class AudioData {

		public string Id { get; set; }
		public string Name { get; set; } 
		public string Driver { get; set; } 
		public bool IsDefault { get; set; } 
		public bool IsEnabled { get; set; } 
		public bool IsInitialized { get; set; } 
		public bool IsLoopback { get; set; } 
		public DeviceType Type { get; set; }

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