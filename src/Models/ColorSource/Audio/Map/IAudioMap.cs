using System.Collections.Generic;

namespace Glimmr.Models.ColorSource.Audio.Map {
	public interface IAudioMap {
		// These tell our app which sector gets which channel value from our colors
		public Dictionary<int, int> LeftSectors { get; set; }
		public Dictionary<int, int> RightSectors { get; set; }

	}
}