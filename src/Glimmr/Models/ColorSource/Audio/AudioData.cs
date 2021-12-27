#region

using ManagedBass;
using Newtonsoft.Json;

#endregion

namespace Glimmr.Models.ColorSource.Audio; 

public class AudioData {
	/// <summary>
	///     Is this the default device?
	/// </summary>
	[JsonProperty]
	public bool IsDefault { get; set; }

	/// <summary>
	///     Is this device enabled?
	/// </summary>
	[JsonProperty]
	public bool IsEnabled { get; set; }

	/// <summary>
	///     Is this a loopback device?
	/// </summary>

	[JsonProperty]
	public bool IsLoopback { get; set; }


	/// <summary>
	///     Same as Device Name.
	/// </summary>
	[JsonProperty]
	public string Id { get; set; } = "";

	/// <summary>
	///     Device Name (Also Device ID).
	/// </summary>
	[JsonProperty]
	public string Name { get; set; } = "";

	public void ParseDevice(DeviceInfo input) {
		Name = input.Name;
		Id = Name;
		IsDefault = input.IsDefault;
		IsEnabled = input.IsEnabled;
	}
}