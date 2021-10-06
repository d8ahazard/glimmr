using System;
using Glimmr.Models.ColorTarget.Led;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Glimmr.Models.ColorTarget {
	public class ColorTargetConverter : JsonConverter<ColorTarget>
	{
		public override void WriteJson(JsonWriter writer, ColorTarget? value, JsonSerializer serializer) {
			
		}

		public override ColorTarget ReadJson(JsonReader reader, Type objectType, ColorTarget existingValue, bool hasExistingValue, JsonSerializer serializer)
		{
			
			var jObject = JObject.Load(reader);
			var typeDiscriminator = (jObject["tag"] ?? throw new InvalidOperationException()).Value<string>();
			switch (typeDiscriminator)
			{
				case "Led":
					return (LedData) serializer.Deserialize<ColorTarget>(reader);              
				case "Piano":
					return serializer.Deserialize<Musician.Pianist>(reader);    
				default:
					throw new NotSupportedException();
			}   
			...
		}
	}
}