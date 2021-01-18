using System.Collections.Generic;

namespace Glimmr.Models.ColorSource.Audio.Map {
	public class AudioMapCorners : IAudioMap {
		public Dictionary<int, int> LeftSectors { get; set; } = new Dictionary<int, int> {
			{14, 30},
			{19, 30},
			{13, 125},
			{20, 125},
			{15, 125},
			{18, 125},
			{12, 250},
			{21, 250},
			{11, 500},
			{22, 500},
			{16, 1000},
			{17, 1000},
			{10, 1000},
			{23, 1000}
		};
		public Dictionary<int, int> RightSectors { get; set; } = new Dictionary<int, int> {
			{0, 30},
			{5, 30},
			{6, 125},
			{27, 125},
			{4, 125},
			{1, 125},
			{7, 250},
			{26, 250},
			{3, 500},
			{2, 500},
			{8, 1000},
			{25, 1000},
			{9, 1000},
			{24, 1000}
		};
		
		public float MinColorRange { get; set; }
		public float MaxColorRange { get; set; }
		public float RotationSpeed { get; } = .2f;
		public float RotationThreshold { get; } = .75f;
	}
}