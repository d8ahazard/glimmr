using Glimmr.Models.ColorSource.Audio;
using Newtonsoft.Json;
using Xunit;

namespace Glimmr.Tests;

public class AudioSceneSerializationTests {
	[Fact]
	public void LegacyOctaveMapSceneDeserializes() {
		const string json = @"{
            ""OctaveMap"": { ""0"": 7, ""1"": 6, ""5"": 3 },
            ""RotationLower"": 0.0,
            ""RotationUpper"": 1.0,
            ""RotationSpeed"": 0.005,
            ""RotationThreshold"": 0.85,
            ""Id"": 2,
            ""Name"": ""Center"",
            ""System"": true
        }";

		var scene = JsonConvert.DeserializeObject<AudioScene>(json);
		Assert.Equal(2, scene.Id);
		Assert.Equal("Center", scene.Name);
		Assert.True(scene.System);
		Assert.True(scene.IsLegacy);
		Assert.Equal("Legacy", scene.EffectiveMode);
		Assert.NotNull(scene.OctaveMap);
		Assert.Equal(3, scene.OctaveMap.Count);
	}

	[Fact]
	public void ModernSceneDeserializes() {
		const string json = @"{
            ""Id"": 12,
            ""Name"": ""Laser Rain"",
            ""System"": true,
            ""Mode"": ""LaserRain"",
            ""Palette"": ""synthwave"",
            ""BandScale"": ""Mel"",
            ""BandCount"": 32,
            ""Sensitivity"": 0.65,
            ""NoiseGate"": 0.06,
            ""Attack"": 0.75,
            ""Decay"": 0.55,
            ""BeatSensitivity"": 0.7,
            ""Motion"": 0.7,
            ""Sparkle"": 0.6,
            ""StrobeLimit"": 0.65
        }";

		var scene = JsonConvert.DeserializeObject<AudioScene>(json);
		Assert.Equal(12, scene.Id);
		Assert.Equal("LaserRain", scene.Mode);
		Assert.False(scene.IsLegacy);
		Assert.Equal("LaserRain", scene.EffectiveMode);
		Assert.Equal("synthwave", scene.Palette);
		Assert.Equal(32, scene.BandCount);
		Assert.InRange(scene.Sensitivity, 0.6f, 0.7f);
	}

	[Fact]
	public void EmptySceneFallsBackToAurora() {
		var scene = new AudioScene();
		Assert.Equal("Aurora", scene.EffectiveMode);
		Assert.False(scene.IsLegacy);
	}

	[Fact]
	public void RoundTripPreservesNewFields() {
		var original = new AudioScene {
			Id = 99,
			Name = "Custom",
			System = false,
			Mode = "Vortex",
			Palette = "synthwave",
			BandScale = "Mel",
			BandCount = 24,
			Sensitivity = 0.65f,
			NoiseGate = 0.05f,
			Attack = 0.5f,
			Decay = 0.3f,
			BeatSensitivity = 0.55f,
			Motion = 0.4f,
			Sparkle = 0.2f,
			StrobeLimit = 0.6f
		};

		var serialized = JsonConvert.SerializeObject(original);
		var deserialized = JsonConvert.DeserializeObject<AudioScene>(serialized);

		Assert.Equal(original.Id, deserialized.Id);
		Assert.Equal(original.Mode, deserialized.Mode);
		Assert.Equal(original.Palette, deserialized.Palette);
		Assert.Equal(original.BandCount, deserialized.BandCount);
		Assert.Equal(original.Motion, deserialized.Motion);
	}
}
