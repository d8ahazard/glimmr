using Emgu.CV.Structure;

namespace Glimmr.Models.ColorSource.Audio {
	public struct AudioScene {
		public RangeF LowRange { get; set; }
		public RangeF MidRange { get; set; }
		public RangeF HighRange { get; set; }
		public bool MirrorRange { get; set; }

		public float RotationLower { get; set; }
		public float RotationUpper { get; set; }
		public float RotationSpeed { get; set; }
		public float RotationThreshold { get; set; }

		public int Id { get; set; }
		public string Name { get; set; }
	}
}