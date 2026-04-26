using System;
using System.Collections.Generic;
using System.Drawing;
using Emgu.CV;
using Emgu.CV.Structure;
using Newtonsoft.Json;

namespace Glimmr.Models.Data;

public class LedLayout3D {
	/// <summary>
	///		 The coordinates of the LED strip in 3D space. These are a series of floats that represent the position of the LED strip in 3D space.
	/// </summary>
	[JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
	public LedCoordinate[] coordinates { get; set; }

	/// <summary>
	///		The position of the LED strip in 3D space, relative to the origin. x/y are the position, z is the scale.
	/// </summary>
	[JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
	public LedCoordinate relativePosition { get; set; }

	/// <summary>
	///		 The rotation of the LED strip in 3D space, relative to the origin, in radians.
	/// </summary>
	[JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
	public LedCoordinate relativeRotation { get; set; }

	/// <summary>
	///		 Flip the pixels along the X axis.
	/// </summary>

	[JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
	public bool FlipX { get; set; }

	/// <summary>
	///		 Flip the pixels along the Y axis.
	/// </summary>
	[JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
	public bool FlipY { get; set; }

	/// <summary>
	///		 When enabled, pixels outside the screen will be rendered with edge colors, otherwise they will be black.
	/// </summary>
	[JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
	public bool ShowEdges { get; set; }

	public LedLayout3D() {
		coordinates = [];
		relativePosition = new LedCoordinate(0, 0, 1);
		relativeRotation = new LedCoordinate(0, 0, 0);
	}

	public LedLayout3D(LedCoordinate[] coords, LedCoordinate position, LedCoordinate rotation) {
		coordinates = coords;
		relativePosition = position;
		relativeRotation = rotation;
	}

	public static LedLayout3D FromTwinklyLayout(Twinkly_xled.JSONModels.LedLayout layout) {
		var position = new LedCoordinate(0, 0, 1);
		var rotation = new LedCoordinate(0, 0, 0); // Assuming no rotation is needed after adjusting the coordinates
		var coords = new LedCoordinate[layout.coordinates.Length];
		for (var i = 0; i < layout.coordinates.Length; i++) {
			// Negate x and y to "invert" the coordinates, assuming that's the needed conversion
			coords[i] = new LedCoordinate(-layout.coordinates[i].x, -layout.coordinates[i].y, layout.coordinates[i].z);
		}

		return new LedLayout3D(coords, position, rotation);
	}


	public LedCoordinate[] GetTranslatedCoordinates() {
		var translatedCoords = new LedCoordinate[coordinates.Length];
		float minX = float.MaxValue, minY = float.MaxValue;
		float maxX = float.MinValue, maxY = float.MinValue;

		// Simulate depth for 2D points by assigning a temporary non-zero Z value
		var tempZValue = 0.1f; // Small value to simulate depth without distorting the layout too much

		// Rotate, Scale, and find Min/Max
		for (var i = 0; i < coordinates.Length; i++) {
			// Apply temporary Z value for 2D points
			var coordWithDepth = new LedCoordinate(coordinates[i].x, coordinates[i].y, coordinates[i].z + tempZValue);
			var rotatedCoord = RotateCoordinate(coordWithDepth, relativeRotation);

			// Project the 3D coordinates back to 2D by ignoring the Z component
			translatedCoords[i] = new LedCoordinate(rotatedCoord.x, rotatedCoord.y, 0);

			// Update min/max values for normalization
			minX = Math.Min(minX, translatedCoords[i].x);
			minY = Math.Min(minY, translatedCoords[i].y);
			maxX = Math.Max(maxX, translatedCoords[i].x);
			maxY = Math.Max(maxY, translatedCoords[i].y);
		}

		// Normalize and scale the projected 2D coordinates
		for (var i = 0; i < translatedCoords.Length; i++) {
			var normalizedX = (translatedCoords[i].x - minX) / (maxX - minX);
			var normalizedY = (translatedCoords[i].y - minY) / (maxY - minY);

			translatedCoords[i] = new LedCoordinate(normalizedX, normalizedY, 0);

			if (FlipX) {
				translatedCoords[i].x = 1 - translatedCoords[i].x;
			}
			if (FlipY) {
				translatedCoords[i].y = 1 - translatedCoords[i].y;
			}
		}

		return translatedCoords;
	}



	private LedCoordinate RotateCoordinate(LedCoordinate coord, LedCoordinate rotation) {
		var cosX = Math.Cos(rotation.x);
		var sinX = Math.Sin(rotation.x);
		var cosY = Math.Cos(rotation.y);
		var sinY = Math.Sin(rotation.y);
		var cosZ = Math.Cos(rotation.z);
		var sinZ = Math.Sin(rotation.z);

		var rx = new[,] { { 1, 0, 0 }, { 0, cosX, -sinX }, { 0, sinX, cosX } };
		var ry = new[,] { { cosY, 0, sinY }, { 0, 1, 0 }, { -sinY, 0, cosY } };
		var rz = new[,] { { cosZ, -sinZ, 0 }, { sinZ, cosZ, 0 }, { 0, 0, 1 } };

		var rotated = MatrixMultiply(MatrixMultiply(MatrixMultiply(rz, ry), rx),
			new[,] { { coord.x }, { coord.y }, { (double)coord.z } });

		return new LedCoordinate((float)rotated[0, 0], (float)rotated[1, 0], (float)rotated[2, 0]);
	}


	private double[,] MatrixMultiply(double[,] a, double[,] b) {
		var result = new double[a.GetLength(0), b.GetLength(1)];
		for (var i = 0; i < result.GetLength(0); i++) {
			for (var j = 0; j < result.GetLength(1); j++) {
				for (var k = 0; k < a.GetLength(1); k++) {
					result[i, j] += a[i, k] * b[k, j];
				}
			}
		}

		return result;
	}


	public Image RenderLayoutOld() {
		var translatedCoords = GetTranslatedCoordinates();
		var relativeScale = Math.Max(0.1, Math.Min(relativePosition.z, 5)); // Ensure scale is within 0.1 to 5

		var screenW = 720 * 2;
		var screenH = 480 * 2;
		var baseBoxSize = 480 * 2; // Original size of the 1x1 ratio box
		var boxSize = (int)(baseBoxSize * relativeScale); // Adjust box size based on relativePosition.z
		var boxMarginX =
			(screenW - boxSize) / 2 + (int)(relativePosition.x * (screenW - boxSize)); // Adjust for relativePosition.x
		// Revised calculation for boxMarginY
		var boxMarginY =
			(screenH - boxSize) / 2 +
			(int)(relativePosition.y * screenH); // Adjust for relativePosition.y using full screen height

		using (var image = new Mat(screenH, screenW, Emgu.CV.CvEnum.DepthType.Cv8U, 3)) {
			image.SetTo(new MCvScalar(0, 0, 0)); // Set background to black

			// Draw LEDs within the scaled and shifted box
			foreach (var coord in translatedCoords) {
				// Scale normalized coordinates to fit within the scaled box and adjust for the new box's position
				var x = (int)(coord.x * boxSize) + boxMarginX;
				var y = (int)(coord.y * boxSize) + boxMarginY;
				// Draw LED as a small circle
				CvInvoke.Circle(image, new Point(x, y), 5, new MCvScalar(0, 0, 255), -1);
			}

			// Convert Emgu.CV.Mat to System.Drawing.Bitmap
			var bitmap = image.ToBitmap();
			return bitmap;
		}
	}
	
	


	// public List<LedCoordinate> RenderPixelLayout() {
	// 	var translatedCoords = GetTranslatedCoordinates();
	// 	var relativeScale = Math.Max(0.1, Math.Min(relativePosition.z, 5)); // Ensure scale is within 0.1 to 5
	//
	// 	var screenW = 720 * 2;
	// 	var screenH = 480 * 2;
	// 	var baseBoxSize = 480 * 2; // Original size of the 1x1 ratio box
	// 	var boxSize = (int)(baseBoxSize * relativeScale); // Adjust box size based on relativePosition.z
	// 	var boxMarginX =
	// 		(screenW - boxSize) / 2 + (int)(relativePosition.x * (screenW - boxSize)); // Adjust for relativePosition.x
	//
	// 	// Apply a different logic for boxMarginY based on the value of z to fix vertical movement issue
	// 	var boxMarginY = relativePosition.z == 1
	// 		? (screenH - baseBoxSize) / 2 + (int)(relativePosition.y * (screenH - baseBoxSize))
	// 		: (screenH - boxSize) / 2 +
	// 		  (int)(relativePosition.y * (screenH - boxSize)); // Adjust for relativePosition.y
	//
	// 	var ledCoordinates = new List<LedCoordinate>();
	//
	// 	foreach (var coord in translatedCoords) {
	// 		// Scale normalized coordinates to fit within the scaled box and adjust for the new box's position
	// 		var x = coord.x * boxSize + boxMarginX;
	// 		var y = coord.y * boxSize + boxMarginY;
	//
	// 		// Calculate the percentage position on the screen
	// 		var xPercent = Math.Max(0, Math.Min(x / screenW, 1));
	// 		var yPercent = Math.Max(0, Math.Min(y / screenH, 1));
	//
	// 		// Determine if the LED is on the edge and should be drawn
	// 		var z = 1;
	// 		if (xPercent == 0 || xPercent == 1 || yPercent == 0 || yPercent == 1) {
	// 			z = ShowEdges ? 1 : 0;
	// 		}
	//
	// 		ledCoordinates.Add(new LedCoordinate(xPercent, yPercent, z));
	// 	}
	//
	// 	return ledCoordinates;
	// }
	
	private LedCoordinate RotateAndFlipCoordinate(LedCoordinate coord) {
		// Apply rotation around the X axis
		var x = coord.x;
		var y = coord.y * (float)Math.Cos(relativeRotation.x) - coord.z * (float)Math.Sin(relativeRotation.x);
		var z = coord.y * (float)Math.Sin(relativeRotation.x) + coord.z * (float)Math.Cos(relativeRotation.x);

		// Apply rotation around the Y axis
		var tempX = x * (float)Math.Cos(relativeRotation.y) + z * (float)Math.Sin(relativeRotation.y);
		var tempZ = -x * (float)Math.Sin(relativeRotation.y) + z * (float)Math.Cos(relativeRotation.y);
		x = tempX;
		z = tempZ;

		// Apply rotation around the Z axis
		tempX = x * (float)Math.Cos(relativeRotation.z) - y * (float)Math.Sin(relativeRotation.z);
		var tempY = x * (float)Math.Sin(relativeRotation.z) + y * (float)Math.Cos(relativeRotation.z);
		x = tempX;
		y = tempY;

		// Apply flipping
		if (FlipX) {
			x = -x;
		}
		if (FlipY) {
			y = -y;
		}

		return new LedCoordinate(x, y, z);
	}

	
	public LedCoordinate[] RenderPixelLayoutOld() {
		var transformedCoordinates = new LedCoordinate[coordinates.Length];

		for (var i = 0; i < coordinates.Length; i++) {
			// Rotate and potentially flip coordinates
			transformedCoordinates[i] = RotateAndFlipCoordinate(coordinates[i]);
		}

		return transformedCoordinates;
	}
	
	
	
	public LedCoordinate[] RenderPixelLayout() {
		var screenDimensions = (width: 640f, height: 480f);
		var screenCoordinates = new LedCoordinate[coordinates.Length];

		// Calculate extents
		float minX = float.MaxValue, minY = float.MaxValue, minZ = float.MaxValue;
		float maxX = float.MinValue, maxY = float.MinValue, maxZ = float.MinValue;

		foreach (var coord in coordinates) {
			var rotatedCoord = RotateAndFlipCoordinate(coord);
			minX = Math.Min(minX, rotatedCoord.x);
			minY = Math.Min(minY, rotatedCoord.y);
			minZ = Math.Min(minZ, rotatedCoord.z);
			maxX = Math.Max(maxX, rotatedCoord.x);
			maxY = Math.Max(maxY, rotatedCoord.y);
			maxZ = Math.Max(maxZ, rotatedCoord.z);
		}

		var width = maxX - minX;
		var height = maxY - minY;
		var depth = maxZ - minZ;

		for (var i = 0; i < coordinates.Length; i++) {
			var rotatedCoord = RotateAndFlipCoordinate(coordinates[i]);

			// Normalize within the bounding box
			var normalizedX = (rotatedCoord.x - minX) / width;
			var normalizedY = (rotatedCoord.y - minY) / height;

			// Project onto 2D screen, applying scaling and translation
			var x = (normalizedX * relativePosition.z + (relativePosition.x / screenDimensions.width)) * screenDimensions.width;
			var y = (normalizedY * relativePosition.z + (relativePosition.y / screenDimensions.height)) * screenDimensions.height;

			// Convert to percentage and clamp to [0, 1]
			x = Math.Clamp(x / screenDimensions.width, 0, 1);
			y = Math.Clamp(y / screenDimensions.height, 0, 1);

			// Determine if edge pixel and set z accordingly
			float z = (x == 0 || x == 1 || y == 0 || y == 1) ? (ShowEdges ? 1 : 0) : 1;

			screenCoordinates[i] = new LedCoordinate(x, y, z);
		}

		return screenCoordinates;
	}
	
	public Image RenderLayout() {
		var screenWidth = 640;
		var screenHeight = 480;
		var viewerDistance = relativePosition.z; // Distance from the viewer to the screen

		// Create a canvas
		var canvas = new Mat(screenHeight, screenWidth, Emgu.CV.CvEnum.DepthType.Cv8U, 3);
		canvas.SetTo(new Bgr(Color.Black).MCvScalar);

		// Get the 2D layout coordinates
		var layoutCoordinates = RenderPixelLayout();

		foreach (var coord in layoutCoordinates) {
			if (coord.z > 0) {
				// Perspective projection
				var perspectiveScale = viewerDistance / (viewerDistance + coord.z);
				var x = (int)((coord.x * perspectiveScale + 0.5f) * screenWidth);
				var y = (int)((coord.y * perspectiveScale + 0.5f) * screenHeight);

				// Draw a small circle for each point
				CvInvoke.Circle(canvas, new Point(x, y), 2, new Bgr(Color.White).MCvScalar, -1);
			}
		}

		// Convert the Mat to Bitmap
		var bitmap = canvas.ToBitmap();

		// Cast the Bitmap to System.Drawing.Image for return
		Image image = bitmap;

		return image;
	}


}