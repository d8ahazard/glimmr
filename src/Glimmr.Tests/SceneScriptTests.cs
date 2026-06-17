using System.Drawing;
using System.Linq;
using Glimmr.Models.ColorSource.Scene.Script;
using Xunit;

namespace Glimmr.Tests;

public class SceneScriptTests {
	private const string Fireworks = @"
@scene fps=30 bg=#000010 trail=0.86 size=32x48

emit rockets
  rate    = 1 per 0.45..0.95s
  origin  = x:rand y:bottom
  vel     = dir:up speed:34..44
  gravity = down 22
  color   = pick(#ffd27f,#ffb24d)
  die     = when vy<=0
  spawn   = burst

emit burst
  count   = 60..110 burst
  origin  = parent
  vel     = dir:radial speed:14..30
  gravity = down 16
  color   = pick(#ff4d4d,#ffd24d,#4dff6a)
  fade    = 1.3s
  spawn   = embers @0.35

emit embers
  count   = 1..3 burst
  origin  = parent
  vel     = dir:radial speed:3..8
  gravity = down 16
  color   = parent
  fade    = 0.7s
";

	[Fact]
	public void ParsesFireworksWithoutErrors() {
		var spec = SceneScriptParser.Parse(Fireworks, out var errors);
		Assert.Empty(errors);
		Assert.Equal(32, spec.Width);
		Assert.Equal(48, spec.Height);
		Assert.Equal(30, spec.Fps);
		Assert.Equal(Color.FromArgb(0, 0, 16), spec.Background);
		Assert.Equal(3, spec.Emitters.Count);
		Assert.Single(spec.RootEmitters);
		Assert.Equal("rockets", spec.RootEmitters[0]);
	}

	[Fact]
	public void ParsesEmitterParameters() {
		var spec = SceneScriptParser.Parse(Fireworks, out _);
		var rockets = spec.Emitters["rockets"];
		Assert.True(rockets.Continuous);
		Assert.Equal(SpawnDirection.Up, rockets.Direction);
		Assert.Equal(34, rockets.Speed.Min);
		Assert.Equal(44, rockets.Speed.Max);
		Assert.Equal(22, rockets.GravityY);
		Assert.True(rockets.DieOnApex);
		Assert.Equal("burst", rockets.SpawnOnDeath);
		Assert.Equal(OriginAxis.Random, rockets.Origin.XMode);
		Assert.Equal(1.0, rockets.Origin.Y); // y:bottom

		var burst = spec.Emitters["burst"];
		Assert.False(burst.Continuous);
		Assert.Equal(SpawnDirection.Radial, burst.Direction);
		Assert.True(burst.Origin.FromParent);
		Assert.Equal("embers", burst.SpawnOnDeath);
		Assert.Equal(0.35, burst.SpawnChance, 3);

		var embers = spec.Emitters["embers"];
		Assert.Equal(ColorMode.Parent, embers.ColorMode);
	}

	[Fact]
	public void ReportsUnknownSpawnReference() {
		const string script = @"
@scene size=8x8
emit a
  rate = 1 per 1s
  spawn = ghost
";
		SceneScriptParser.Parse(script, out var errors);
		Assert.Contains(errors, e => e.Contains("ghost"));
	}

	[Fact]
	public void RequiresARootEmitter() {
		const string script = @"
@scene size=8x8
emit a
  count = 5 burst
";
		SceneScriptParser.Parse(script, out var errors);
		Assert.Contains(errors, e => e.Contains("root emitter"));
	}

	[Fact]
	public void ReportsInvalidColor() {
		const string script = @"
emit a
  rate = 1 per 1s
  color = notacolor
";
		SceneScriptParser.Parse(script, out var errors);
		Assert.NotEmpty(errors);
	}
}

public class ParticleEngineTests {
	[Fact]
	public void ProducesAFilledGridAndSpawnsParticles() {
		const string script = @"
@scene fps=30 bg=#000000 trail=0.9 size=16x16
emit rain
  rate    = 40 per 0.001s
  origin  = x:rand y:top
  vel     = dir:down speed:10..14
  gravity = down 5
  color   = #ffffff
  fade    = 2s
";
		var spec = SceneScriptParser.Parse(script, out var errors);
		Assert.Empty(errors);
		var engine = new ParticleEngine(spec);
		var grid = new Color[spec.Width * spec.Height];

		// Run a couple seconds of frames; particles should light up the grid.
		var lit = 0;
		for (var f = 0; f < 90; f++) {
			engine.NextFrame(grid);
			lit = grid.Count(c => c.R > 0 || c.G > 0 || c.B > 0);
		}

		Assert.True(lit > 0, "Engine never lit any cells.");
		Assert.Equal(spec.Width * spec.Height, grid.Length);
	}

	[Fact]
	public void GravityPullsParticlesDownward() {
		// One slow burst at the top with downward gravity; its average row should descend.
		const string script = @"
@scene fps=30 bg=#000000 trail=0 size=8x32
emit drop
  rate    = 1 per 100s
  origin  = x:center y:top
  vel     = dir:down speed:2
  gravity = down 30
  color   = #ffffff
  life    = 100s
";
		var spec = SceneScriptParser.Parse(script, out _);
		var engine = new ParticleEngine(spec);
		var grid = new Color[spec.Width * spec.Height];

		int LowestLitRow() {
			var row = -1;
			for (var y = 0; y < spec.Height; y++) {
				for (var x = 0; x < spec.Width; x++) {
					if (grid[y * spec.Width + x].R > 0) {
						row = y;
					}
				}
			}

			return row;
		}

		engine.NextFrame(grid);
		var early = LowestLitRow();
		for (var f = 0; f < 20; f++) {
			engine.NextFrame(grid);
		}

		var later = LowestLitRow();
		Assert.True(later > early, $"Particle did not fall (early={early}, later={later}).");
	}
}
