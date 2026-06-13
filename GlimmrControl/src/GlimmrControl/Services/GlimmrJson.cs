using System.Text.Json;

namespace GlimmrControl.Services;

/// <summary>Shared serialization settings matching the Glimmr server's camelCase contract.</summary>
public static class GlimmrJson
{
	public static readonly JsonSerializerOptions Options = new()
	{
		PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
		PropertyNameCaseInsensitive = true,
		DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.Never
	};
}
