using System.Collections.Generic;

namespace Glimmr.Models.ColorSource.Audio.Map {
	public class AudioMapTop : IAudioMap {
		public Dictionary<int, int> LeftSectors { get; set; } = new Dictionary<int, int> {
			{10, 30},
			{11, 30},
			{12, 60},
			{13, 60},
			{14, 125},
			{15, 125},
			{16, 250},
			{17, 250},
			{18, 500},
			{19, 500},
			{20, 1000},
			{21, 1000},
			{22, 2000},
			{23, 2000}
		};
		public Dictionary<int, int> RightSectors { get; set; } = new Dictionary<int, int> {
			{9, 30},
			{8, 30},
			{7, 60},
			{6, 60},
			{5, 125},
			{4, 125},
			{3, 250},
			{2, 250},
			{1, 500},
			{0, 500},
			{27, 1000},
			{26, 1000},
			{25, 2000},
			{24, 2000}
		};

		public float MinColorRange { get; set; } = 0;
		public float MaxColorRange { get; set; } = 1;
		public float RotationSpeed { get; }
	}
}