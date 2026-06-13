using System.Text.Json.Serialization;

namespace GlimmrControl.Models;

/// <summary>An ambient or audio scene entry (id + display name).</summary>
public class Scene
{
	[JsonPropertyName("id")] public int Id { get; set; }
	[JsonPropertyName("name")] public string Name { get; set; } = "";
}
