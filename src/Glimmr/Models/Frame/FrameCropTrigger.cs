#region

using System;

#endregion

namespace Glimmr.Models.Frame;

public class FrameCropTrigger {
	public bool Triggered => _count >= _max;

	private readonly int _max;

	private int _count;

	private int _dimension;

	public FrameCropTrigger(int max) {
		_max = max;
		_count = 0;
	}

	public void Clear() {
		_count = 0;
	}


	public void Tick(int dim) {
		// If our dimension is what we're already saving, count up.
		if (dim == 0) {
			_count = 0;
			return;
		}

		if (Math.Abs(dim - _dimension) <= 2) {
			if (_count < _max + 4) {
				_count++;
			}
		} else {
			if (_count > 0) {
				_count--;
			}
		}

		if (_count < _max) {
			_dimension = dim;
		}
	}
}