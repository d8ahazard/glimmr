#region

using Newtonsoft.Json;

#endregion

namespace Glimmr.Enums;

public enum DeviceUnits {
	/// <summary>
	///     Imperial
	/// </summary>
	[JsonProperty] Imperial = 0,

	/// <summary>
	///     Metric
	/// </summary>
	[JsonProperty] Metric = 1
}