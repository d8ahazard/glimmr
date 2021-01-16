using System.Collections.Generic;

namespace Glimmr.Models.ColorSource.Audio.Map {
	public class AudioMapBottom : IAudioMap {
		public Dictionary<int, int> LeftSectors { get; set; } = new Dictionary<int, int> {
			{23, 30},
			{22, 30},
			{21, 60},
			{20, 60},
			{19, 125},
			{18, 125},
			{17, 250},
			{16, 250},
			{15, 500},
			{14, 500},
			{13, 1000},
			{12, 1000},
			{11, 2000},
			{10, 2000}
		};
		public Dictionary<int, int> RightSectors { get; set; } = new Dictionary<int, int> {
			{24, 30},
			{25, 30},
			{26, 60},
			{27, 60},
			{0, 125},
			{1, 125},
			{2, 250},
			{3, 250},
			{4, 500},
			{5, 500},
			{6, 1000},
			{7, 1000},
			{8, 2000},
			{9, 2000}
		};

		public float MinColorRange { get; set; } = 0;
		public float MaxColorRange { get; set; } = 1;
		public float RotationSpeed { get; } = .005f;
		public float RotationThreshold { get; } = 1f;
	}
}