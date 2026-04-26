#region

using System.Drawing;

#endregion

namespace Glimmr.Models.ColorSource.Audio.Engine;

/// <summary>
///     A renderer that turns an <see cref="AudioReactiveState" /> snapshot
///     into 20 perimeter colors. Effects are stateful (rotation phases,
///     particle systems, decay buffers, etc.) but always own that state
///     internally; the caller never has to pass it back in.
/// </summary>
public interface IAudioEffect {
	string Name { get; }

	/// <summary>
	///     Reset all internal animation state (called when scenes change or
	///     audio resumes after a long silence).
	/// </summary>
	void Reset();

	/// <summary>
	///     Render one frame into <paramref name="output" /> (length 20).
	///     Implementations must always fill every slot; black slots should
	///     be written explicitly.
	/// </summary>
	void Render(AudioReactiveState state, AudioScene scene, Color[] output);
}
