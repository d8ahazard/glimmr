using System.Drawing;
using Glimmr.Models.ColorSource.Audio.Engine;
using Xunit;

namespace Glimmr.Tests;

public class AudioPaletteTests {
	[Fact]
	public void BuiltInPaletteResolves() {
		var p = AudioPalette.Get("aurora");
		Assert.Equal("aurora", p.Name);
	}

	[Fact]
	public void EmptyNameUsesDefault() {
		var p = AudioPalette.Get(null);
		Assert.NotNull(p);
		var c = p.Sample(0.0f);
		Assert.True(c.A == 255);
	}

	[Fact]
	public void HexStopsParsedFromInlineString() {
		var p = AudioPalette.Get("#ff0000,#00ff00");
		var start = p.Sample(0.0f);
		var end = p.Sample(0.999f);
		Assert.Equal(255, start.R);
		Assert.True(end.G > 200);
	}

	[Fact]
	public void SampleHandlesWraparound() {
		var p = AudioPalette.Get("aurora");
		var a = p.Sample(0.5f);
		var b = p.Sample(1.5f);
		Assert.Equal(a, b);
	}

	[Fact]
	public void SinglePaletteStopReturnsThatStop() {
		var p = new AudioPalette("solo", new[] { Color.FromArgb(255, 10, 20, 30) });
		var c = p.Sample(0.7f);
		Assert.Equal(10, c.R);
		Assert.Equal(20, c.G);
		Assert.Equal(30, c.B);
	}
}
