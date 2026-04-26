using Glimmr.Models.ColorSource.Audio.Engine;
using Xunit;

namespace Glimmr.Tests;

public class PerimeterLayoutTests {
	[Fact]
	public void SlotCountIsTwenty() {
		Assert.Equal(20, PerimeterLayout.SlotCount);
		Assert.Equal(PerimeterLayout.SlotCount,
			PerimeterLayout.RightLength + PerimeterLayout.TopLength + PerimeterLayout.LeftLength +
			PerimeterLayout.BottomLength);
	}

	[Theory]
	[InlineData(0, 0.025f)]
	[InlineData(10, 0.525f)]
	[InlineData(19, 0.975f)]
	public void ThetaForSlotIsCenterOfSlot(int slot, float expected) {
		Assert.Equal(expected, PerimeterLayout.ThetaForSlot(slot), 3);
	}

	[Fact]
	public void SlotWeightPeaksAtCenter() {
		var w = PerimeterLayout.SlotWeight(5, 0.275f, 4f);
		Assert.True(w > 0.95f, $"Expected near-1 weight at center, got {w}");
	}

	[Fact]
	public void SlotWeightFallsOff() {
		var center = PerimeterLayout.SlotWeight(5, 0.275f, 1f);
		var far = PerimeterLayout.SlotWeight(5, 0.025f, 1f);
		Assert.True(far < center);
	}

	[Fact]
	public void SlotWeightWrapsAroundPerimeter() {
		// Slot 0 is at theta ~0.025; a center near 0.975 should still have
		// non-trivial weight on slot 0 thanks to wrap-around.
		var w = PerimeterLayout.SlotWeight(0, 0.975f, 2f);
		Assert.True(w > 0.4f, $"Expected wrap-around to help slot 0, got {w}");
	}
}
