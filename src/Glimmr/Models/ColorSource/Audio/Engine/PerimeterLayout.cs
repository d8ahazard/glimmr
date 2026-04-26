#region

using System;

#endregion

namespace Glimmr.Models.ColorSource.Audio.Engine;

/// <summary>
///     Helpers for the 20-slot virtual perimeter that
///     <see cref="Frame.FrameBuilder" /> consumes from the audio path.
///     Slot order matches the FrameBuilder's wedge order: right side
///     (4 slots) -> top (6 slots) -> left side (4 slots) -> bottom (6 slots).
///     Effects use these helpers to position events around the TV instead
///     of indexing arrays by hand.
/// </summary>
public static class PerimeterLayout {
	public const int SlotCount = 20;
	public const int RightStart = 0;
	public const int RightLength = 4;
	public const int TopStart = 4;
	public const int TopLength = 6;
	public const int LeftStart = 10;
	public const int LeftLength = 4;
	public const int BottomStart = 14;
	public const int BottomLength = 6;

	/// <summary>
	///     Map a 0..1 position around the perimeter (clockwise, 0 = top-right
	///     corner) into the slot index space.
	/// </summary>
	public static float SlotFromTheta(float theta) {
		theta -= MathF.Floor(theta);
		if (theta < 0f) {
			theta += 1f;
		}

		return theta * SlotCount;
	}

	/// <summary>
	///     Returns 0..1 progress for a given slot index (slot 0 = right side
	///     near top, wraps around clockwise).
	/// </summary>
	public static float ThetaForSlot(int slot) {
		return (slot + 0.5f) / SlotCount;
	}

	/// <summary>
	///     Returns a triangle-shaped weight (0..1) for slot <paramref name="i" />
	///     centered on a 0..1 perimeter position with a half-width
	///     <paramref name="halfWidthSlots" />.
	/// </summary>
	public static float SlotWeight(int i, float center01, float halfWidthSlots) {
		var slotPos = (i + 0.5f) / SlotCount;
		var d = slotPos - center01;
		if (d > 0.5f) {
			d -= 1f;
		} else if (d < -0.5f) {
			d += 1f;
		}

		var dSlots = MathF.Abs(d) * SlotCount;
		var w = 1f - dSlots / MathF.Max(0.0001f, halfWidthSlots);
		return w < 0f ? 0f : w;
	}
}
