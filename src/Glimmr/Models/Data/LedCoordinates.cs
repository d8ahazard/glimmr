using Newtonsoft.Json;

namespace Glimmr.Models.Data;


public class LedCoordinate {
	[JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
	public float x { get; set; }
	
	[JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
	public float y { get; set; }
	
	[JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
	public float z { get; set; }

	// Parameterless constructor
	public LedCoordinate() {
	}

	public LedCoordinate(float x, float y, float z) {
		this.x = x;
		this.y = y;
		this.z = z;
	}
}
