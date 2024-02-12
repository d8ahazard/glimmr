#region

using Newtonsoft.Json;

#endregion

namespace Glimmr.Enums;

public enum ShapeType {
	Triangle = 0,
	Rhythm = 1,
	Square = 2,
	ControlSquareMaster = 3,
	ControlSquarePassive = 4,
	HexagonShapes = 7,
	TriangleShapes = 8,
	MiniTriangleShapes = 9,
	ControllerShapes = 10
}

public enum ShapeSize {
	[JsonProperty] Triangle = 150,
	[JsonProperty] Rhythm = 0,
	[JsonProperty] Square = 100,
	[JsonProperty] ControlSquareMaster = 100,
	[JsonProperty] ControlSquarePassive = 100,
	[JsonProperty] HexagonShapes = 67,
	[JsonProperty] TriangleShapes = 134,
	[JsonProperty] MiniTriangleShapes = 67,
	[JsonProperty] ShapesController = 0
}