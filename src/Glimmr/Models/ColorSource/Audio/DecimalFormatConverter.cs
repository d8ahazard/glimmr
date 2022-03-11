#region

using System;
using Newtonsoft.Json;

#endregion

namespace Glimmr.Models.ColorSource.Audio;

public class DecimalFormatConverter : JsonConverter {
	public override bool CanRead => false;

	public override bool CanConvert(Type objectType) {
		return objectType == typeof(decimal) || objectType == typeof(float) || objectType == typeof(double);
	}

	public override void WriteJson(JsonWriter writer, object? value,
		JsonSerializer serializer) {
		writer.WriteValue($"{value:N10}");
	}

	public override object ReadJson(JsonReader reader, Type objectType,
		object? existingValue, JsonSerializer serializer) {
		throw new NotImplementedException();
	}
}