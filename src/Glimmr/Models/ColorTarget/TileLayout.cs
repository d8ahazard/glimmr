#region

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using Glimmr.Enums;
using LifxNetPlus;
using Nanoleaf.Client.Models.Responses;
using Newtonsoft.Json;

#endregion

namespace Glimmr.Models.ColorTarget;

[Serializable]
public class TileLayout {
	/// <summary>
	///     Number of panels in the layout.
	/// </summary>
	[JsonProperty]
	public int NumPanels { get; set; }

	/// <summary>
	///     Length of sides of each panel. This is not used for shapes.
	/// </summary>
	[JsonProperty]
	public int SideLength { get; set; }

	/// <summary>
	///     Array of position data for our tiles.
	/// </summary>

	[JsonProperty]
	public TileData[]? PositionData { get; set; }

	public TileLayout() {
	}

	public TileLayout(Layout layout) {
		NumPanels = layout.NumPanels;
		SideLength = layout.SideLength;
		PositionData = layout.PositionData.Select(l => new TileData(l)).ToArray();
	}

	public TileLayout(StateDeviceChainResponse layout) {
		NumPanels = layout.TotalCount;
		var pos = new List<TileData>();
		var idx = 0;
		foreach (var pd in layout.Tiles) {
			pos.Add(new TileData(pd, idx));
			idx++;
		}

		PositionData = pos.ToArray();
	}

	public void MergeLayout(TileLayout newLayout) {
		if (newLayout == null) {
			throw new ArgumentException("Invalid argument.");
		}

		if (PositionData == null && newLayout.PositionData != null) {
			PositionData = newLayout.PositionData;
			return;
		}

		if (newLayout.PositionData == null || PositionData == null) {
			return;
		}

		var posData = new TileData[newLayout.PositionData.Length];
		// Loop through each panel in the new position data, find existing info and copy
		for (var i = 0; i < newLayout.PositionData.Length; i++) {
			var nl = newLayout.PositionData[i];
			foreach (var el in PositionData.Where(s => s.PanelId == nl.PanelId)) {
				nl.TargetSector = el.TargetSector;
			}

			posData[i] = nl;
		}

		NumPanels = newLayout.NumPanels;
		SideLength = newLayout.SideLength;
		PositionData = posData;
	}
}

[Serializable]
public class TileData {
	/// <summary>
	///     Orientation.
	/// </summary>
	[JsonProperty]
	public int O { get; set; }

	/// <summary>
	///     Panel ID.
	/// </summary>
	[JsonProperty]
	public int PanelId { get; set; }

	/// <summary>
	///     What type of shape is this?
	/// </summary>
	[JsonProperty]
	public int ShapeType { get; set; }

	/// <summary>
	///     Length of panel sides.
	/// </summary>
	[JsonProperty]
	public int SideLength { get; set; }

	/// <summary>
	///     The sector to use for streaming for this panel.
	/// </summary>
	[DefaultValue(-1)]
	[JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
	public int TargetSector { get; set; } = -1;

	/// <summary>
	///     Tile X position.
	/// </summary>
	[JsonProperty]
	public int X { get; set; }


	/// <summary>
	///     Tile Y position.
	/// </summary>
	[JsonProperty]
	public int Y { get; set; }

	public TileData() {
	}

	public TileData(Tile t, int index) {
		X = (int)t.UserX;
		Y = (int)t.UserY;
		PanelId = index;
		ShapeType = 2;
		SetSideLength();
	}

	public TileData(PanelLayout layout) {
		PanelId = layout.PanelId;
		X = layout.X;
		Y = layout.Y;
		O = layout.O;
		ShapeType = layout.ShapeType;
		SetSideLength();
	}

	private void SetSideLength() {
		switch ((ShapeType)ShapeType) {
			case Enums.ShapeType.Square:
			case Enums.ShapeType.ControlSquareMaster:
			case Enums.ShapeType.ControlSquarePassive:
				SideLength = (int)ShapeSize.Square;
				break;
			case Enums.ShapeType.Triangle:
				SideLength = (int)ShapeSize.Triangle;
				break;
			case Enums.ShapeType.HexagonShapes:
			case Enums.ShapeType.MiniTriangleShapes:
				SideLength = (int)ShapeSize.HexagonShapes;
				break;
			case Enums.ShapeType.TriangleShapes:
				SideLength = (int)ShapeSize.TriangleShapes;
				break;
			case Enums.ShapeType.Rhythm:
			case Enums.ShapeType.ShapesController:
			default:
				SideLength = 0;
				break;
		}
	}
}