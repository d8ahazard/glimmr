using System.Collections.Generic;

namespace Glimmr.Models.ColorSource.Audio.Map {
	public interface IAudioMap {
		public Dictionary<int, int> LeftSectors { get; set; }
		public Dictionary<int, int> RightSectors { get; set; }
		public float MinColorRange { get; set; }
		public float MaxColorRange { get; set; }
		
		public float RotationSpeed { get; }
	}
}