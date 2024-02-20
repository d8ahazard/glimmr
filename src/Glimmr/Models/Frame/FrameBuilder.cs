#region

using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using Emgu.CV;
using Emgu.CV.CvEnum;
using Emgu.CV.Structure;
using Emgu.CV.Util;
using Newtonsoft.Json;
using Serilog;
using static Glimmr.Models.Constant.GlimmrConstants;

#endregion

namespace Glimmr.Models.Frame;

public class FrameBuilder : IDisposable {
	private int _bottomCount;
	private bool _center;
	private Rectangle[] _centerCoords;
	private bool _disposed;

	// This will store the coords of input values
	private VectorOfVectorOfPoint _inputCoords;
	private int _ledCount;
	private int _leftCount;
	private int _rightCount;
	private int _topCount;
	private bool _updating;

	public FrameBuilder(IReadOnlyList<int> inputDimensions, bool sectors = false, bool center = false) {
		_leftCount = inputDimensions[0];
		_rightCount = inputDimensions[1];
		_topCount = inputDimensions[2];
		_bottomCount = inputDimensions[3];
		_centerCoords = Array.Empty<Rectangle>();
		_center = center;
		_ledCount = _leftCount + _rightCount + _topCount + _bottomCount;
		_inputCoords = new VectorOfVectorOfPoint();
		if (sectors) {
			if (center) {
				_ledCount = _leftCount * _topCount;
				_centerCoords = DrawCenterSectors();
			} else {
				_ledCount -= 4;
				_inputCoords = DrawSectors();
			}
		} else {
			_inputCoords = DrawLeds();
		}
	}

	public void Dispose() {
		_disposed = true;
		((IDisposable)_inputCoords).Dispose();
		GC.SuppressFinalize(this);
	}

	public void Update(IReadOnlyList<int> inputDimensions, bool sectors = false, bool center = false) {
		_updating = true;
		_leftCount = inputDimensions[0];
		_rightCount = inputDimensions[1];
		_topCount = inputDimensions[2];
		_bottomCount = inputDimensions[3];
		_centerCoords = Array.Empty<Rectangle>();
		_center = center;
		_ledCount = _leftCount + _rightCount + _topCount + _bottomCount;
		_inputCoords = new VectorOfVectorOfPoint();
		if (sectors) {
			if (center) {
				_ledCount = _leftCount * _topCount;
				_centerCoords = DrawCenterSectors();
			} else {
				_ledCount -= 4;
				_inputCoords = DrawSectors();
			}
		} else {
			_inputCoords = DrawLeds();
		}

		_updating = false;
	}


	public Mat? Build(IEnumerable<Color> colors) {
		if (_disposed) {
			return null;
		}

		if (_updating) {
			return null;
		}

		var enumerable = colors as Color[] ?? colors.ToArray();
		if (enumerable.Length != _ledCount) {
			throw new ArgumentOutOfRangeException(
				$"Color length should be {_ledCount} versus {enumerable.Length}.");
		}

		var idx = 0;

		try {
			var gMat = new Mat(new Size(ScaleWidth, ScaleHeight), DepthType.Cv8U, 3);
			for (var i = 0; i < enumerable.Length; i++) {
				idx = i;
				var color = enumerable[i];
				var col = new MCvScalar(color.B, color.G, color.R);
				if (_center) {
					CvInvoke.Rectangle(gMat, _centerCoords[i], col, -1);
				} else {
					CvInvoke.DrawContours(gMat, _inputCoords, i, col, -1);
					//CvInvoke.FillPoly(gMat,_inputCoords[i],col,LineType.AntiAlias);	
				}
			}

			//CvInvoke.GaussianBlur(gMat, gMat, new Size(29,29), 0);
			return gMat;
		} catch (Exception e) {
			Log.Debug($"Exception, input coords are {_inputCoords.Size} but requested {idx}: " + e.Message + " at " +
			          e.StackTrace + " " + JsonConvert.SerializeObject(e));
		}

		return null;
	}


	private VectorOfVectorOfPoint DrawLeds() {
		var polly = new VectorOfVectorOfPoint();
		var center = new Point(ScaleWidth / 2, ScaleHeight / 2);

		// Pre-calculate segment sizes
		var segmentWidths = new[] {
			(int)Math.Round((float)ScaleWidth / _topCount, MidpointRounding.AwayFromZero),
			(int)Math.Round((float)ScaleWidth / _bottomCount, MidpointRounding.AwayFromZero)
		};

		var segmentHeights = new[] {
			(int)Math.Round((float)ScaleHeight / _leftCount, MidpointRounding.AwayFromZero),
			(int)Math.Round((float)ScaleHeight / _rightCount, MidpointRounding.AwayFromZero)
		};

		// Calculate points for each side and add them to 'polly'
		AddSidePoints(polly, center, ScaleWidth, segmentHeights[1], _rightCount, false, true);
		AddSidePoints(polly, center, 0, segmentWidths[0], _topCount, true, true);
		AddSidePoints(polly, center, 0, segmentHeights[0], _leftCount, false, false);
		AddSidePoints(polly, center, ScaleHeight, segmentWidths[1], _bottomCount, true, false);
		

		return polly;
	}

	private static void AddSidePoints(VectorOfVectorOfPoint polly, Point center, int start, int segmentSize, int count, bool horizontal, bool reverse) {
		if (reverse) {
			for (var i = count; i >= 0; i--) {
				var ord = i * segmentSize;
				var c2 = ord + segmentSize;

				var pts = horizontal ? new[] { new Point(ord, start), new Point(c2, start), center } : new[] { new Point(start, ord), new Point(start, c2), center };

				polly.Push(new VectorOfPoint(pts));
			}
		} else {
			for (var i = 0; i < count; i++) {
				var ord = i * segmentSize;
				var c2 = ord + segmentSize;

				var pts = horizontal ? new[] { new Point(ord, start), new Point(c2, start), center } : new[] { new Point(start, ord), new Point(start, c2), center };

				polly.Push(new VectorOfPoint(pts));
			}
		}
	}


	private VectorOfVectorOfPoint DrawSectors() {
        var polly = new VectorOfVectorOfPoint();
        var center = new Point(ScaleWidth / 2, ScaleHeight / 2);
        int[] sides = { _rightCount, _topCount - 1, _leftCount, _bottomCount - 1 }; // Adjust counts for corners
        int[] dimensions = { ScaleHeight, ScaleWidth, ScaleHeight, ScaleWidth }; // Heights and widths for each side
        bool[] isHorizontal = { false, true, false, true }; // Orientation of each side
    
        for (var side = 0; side < 4; side++) {
            var stepSize = dimensions[side] / sides[side]; // Step size for the current side
            for (var i = 0; i < sides[side]; i++) {
                var ord = i * stepSize; // Calculate the ordinal position for the current step
                var pts = new Point[3];
    
                if (isHorizontal[side]) {
                    pts[0] = new Point(ord, side == 1 ? 0 : ScaleHeight);
                    pts[1] = new Point(Math.Min(ord + stepSize, ScaleWidth), side == 1 ? 0 : ScaleHeight);
                } else {
                    pts[0] = new Point(side == 2 ? 0 : ScaleWidth, ord);
                    pts[1] = new Point(side == 2 ? 0 : ScaleWidth, Math.Min(ord + stepSize, ScaleHeight));
                }
                pts[2] = center;
    
                polly.Push(new VectorOfPoint(pts));
            }
        }
    
        return polly;
    }


	private Rectangle[] DrawCenterSectors() {
        var rectangles = new List<Rectangle>();
        var sectorWidth = ScaleWidth / _topCount;
        var sectorHeight = ScaleHeight / _leftCount;
    
        for (var v = 0; v < _leftCount; v++) {
            var top = v * sectorHeight;
            for (var h = 0; h < _topCount; h++) {
                var left = h * sectorWidth;
                var rect = new Rectangle(left, top, sectorWidth, sectorHeight);
                rectangles.Add(rect);
            }
        }
    
        return rectangles.ToArray();
    }

}