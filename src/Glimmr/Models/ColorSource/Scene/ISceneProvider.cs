#region

using System.Drawing;

#endregion

namespace Glimmr.Models.ColorSource.Scene;

/// <summary>
///     A source of animation frames for <see cref="SceneStream" />. Implementations either
///     generate frames live (<see cref="Script.ParticleEngine" />) or replay a baked sequence
///     (<see cref="FramePlayer" />). Frames are written into a caller-owned, reused buffer to
///     keep the per-frame path allocation-free.
/// </summary>
public interface ISceneProvider {
	int Width { get; }
	int Height { get; }
	double Fps { get; }

	/// <summary>
	///     Render the next frame into <paramref name="grid" /> (length Width*Height, row-major
	///     from the top-left corner).
	/// </summary>
	void NextFrame(Color[] grid);
}
