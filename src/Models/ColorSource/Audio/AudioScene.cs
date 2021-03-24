using Emgu.CV.Structure;

namespace Glimmr.Models.ColorSource.Audio {
	public struct AudioScene {
		/// <summary>
		/// Perimeter range where low frequencies should be mapped, as a percent.
		/// </summary>
		public RangeF LowRange { get; set; }
		
		/// <summary>
		/// Perimeter range where mid frequencies should be mapped, as a percent.
		/// </summary>
		public RangeF MidRange { get; set; }
		
		/// <summary>
		/// Perimeter range where high frequencies should be mapped, as a percent.
		/// </summary>
		public RangeF HighRange { get; set; }
		
		/// <summary>
		/// Enable me, figure out how to work this
		/// </summary>
		public bool MirrorRange { get; set; }

		/// <summary>
		/// Overall lower limit to color range (0 - 1)
		/// If lower is GEQ higher, will be ignored
		/// </summary>
		public float RotationLower { get; set; }
		
		/// <summary>
		/// Overall upper limit to color range (0 - 1)
		/// If lower is GEQ higher, will be ignored
		/// </summary>
		public float RotationUpper { get; set; }
		
		/// <summary>
		/// How many degrees to rotate on each trigger (0 - 1)
		/// </summary>
		public float RotationSpeed { get; set; }
		
		/// <summary>
		/// Minimum amplitude to trigger color rotation
		/// </summary>
		public float RotationThreshold { get; set; }
		
		public int Id { get; set; }
		public string Name { get; set; }
	}
}