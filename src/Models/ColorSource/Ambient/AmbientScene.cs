using Newtonsoft.Json;

namespace Glimmr.Models.ColorSource.Ambient {
	public struct AmbientScene {
		[JsonProperty]public string[] Colors;
		[JsonProperty]public float AnimationTime;
		[JsonProperty]public string Mode;
		[JsonProperty]public string Easing;
		[JsonProperty]public float EasingTime;
		[JsonProperty]public string Name;
		[JsonProperty]public int Id;
	}
}