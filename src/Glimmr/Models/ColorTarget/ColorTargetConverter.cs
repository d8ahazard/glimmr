using System;
using Glimmr.Models.ColorTarget.Adalight;
using Glimmr.Models.ColorTarget.DreamScreen;
using Glimmr.Models.ColorTarget.Glimmr;
using Glimmr.Models.ColorTarget.Hue;
using Glimmr.Models.ColorTarget.Led;
using Glimmr.Models.ColorTarget.Lifx;
using Glimmr.Models.ColorTarget.Nanoleaf;
using Glimmr.Models.ColorTarget.OpenRgb;
using Glimmr.Models.ColorTarget.Wled;
using Glimmr.Models.ColorTarget.Yeelight;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Glimmr.Models.ColorTarget {
	public class ColorTargetConverter : JsonConverter<IColorTargetData>
	{
		
		public override void WriteJson(JsonWriter writer, IColorTargetData? value, JsonSerializer serializer) {
			
		}

		public override IColorTargetData? ReadJson(JsonReader reader, Type objectType, IColorTargetData? existingValue,
			bool hasExistingValue, JsonSerializer serializer) {
			
			var jObject = JObject.Load(reader);
			var typeDiscriminator = (jObject["tag"] ?? throw new InvalidOperationException()).Value<string>();
			return typeDiscriminator switch {
				"Adalight" => serializer.Deserialize<AdalightData>(reader),
				"DreamScreen" => serializer.Deserialize<DreamScreenData>(reader),
				"Glimmr" => serializer.Deserialize<GlimmrData>(reader),
				"Hue" => serializer.Deserialize<HueData>(reader),
				"Led" => serializer.Deserialize<LedData>(reader),
				"Lifx" => serializer.Deserialize<LifxData>(reader),
				"Nanoleaf" => serializer.Deserialize<NanoleafData>(reader),
				"OpenRgb" => serializer.Deserialize<OpenRgbData>(reader),
				"Wled" => serializer.Deserialize<WledData>(reader),
				"Yeelight" => serializer.Deserialize<YeelightData>(reader),
				_ => throw new NotSupportedException()
			};
		}
	}
}