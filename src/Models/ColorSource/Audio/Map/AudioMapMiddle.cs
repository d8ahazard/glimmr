using System.Collections.Generic;

namespace Glimmr.Models.ColorSource.Audio.Map {
	public class AudioMapMiddle : IAudioMap {
		public Dictionary<int, int> LeftSectors { get; set; } = new Dictionary<int, int> {
			{16, 30},
			{17, 30},
			{15, 60},
			{18, 60},
			{14, 125},
			{19, 125},
			{13, 250},
			{20, 250},
			{12, 500},
			{21, 500},
			{11, 1000},
			{24, 1000},
			{10, 2000},
			{25, 2000}
		};
		public Dictionary<int, int> RightSectors { get; set; } = new Dictionary<int, int> {
			{2, 30},
			{3, 30},
			{4, 60},
			{1, 60},
			{5, 125},
			{0, 125},
			{6, 250},
			{27, 250},
			{7, 500},
			{26, 500},
			{8, 1000},
			{25, 1000},
			{9, 2000},
			{24, 2000}
		};

		public float MinColorRange { get; set; } = 0;
		public float MaxColorRange { get; set; } = 1;
		public float RotationSpeed { get; }
	}
}