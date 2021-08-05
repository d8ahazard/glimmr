#region

using System.Collections.Generic;
using Emgu.CV.Aruco;
using Emgu.CV.Structure;

#endregion

namespace Glimmr.Models.ColorSource.Audio {
	public struct AudioScene {
		/// <summary>
		///     Overall lower limit to color range (0 - 1)
		///     If lower is GEQ higher, will be ignored
		/// </summary>
		public float RotationLower { get; set; }

		/// <summary>
		///     Overall upper limit to color range (0 - 1)
		///     If lower is GEQ higher, will be ignored
		/// </summary>
		public float RotationUpper { get; set; }

		/// <summary>
		///     How many degrees to rotate on each trigger (0 - 1)
		/// </summary>
		public float RotationSpeed { get; set; }

		/// <summary>
		///     Minimum amplitude to trigger color rotation
		/// </summary>
		public float RotationThreshold { get; set; }

		public Dictionary<string, int> OctaveMap { get; set; }
		public int Id { get; set; }
		public string Name { get; set; }
	}
}