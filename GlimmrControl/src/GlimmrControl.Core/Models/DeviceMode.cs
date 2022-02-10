namespace GlimmrControl.Core.Models {
	/// <summary>
	///     The current device mode.
	///     Off = 0
	///     Video = 1
	///     Audio = 2
	///     Ambient = 3
	///     AudioVideo = 4
	///     Udp = 5
	///     DreamScreen = 6
	/// </summary>
	public enum DeviceMode {
		/// <summary>
		///     Off
		/// </summary>
		Off = 0,

		/// <summary>
		///     Video
		/// </summary>
		Video = 1,

		/// <summary>
		///     Audio
		/// </summary>
		Audio = 2,

		/// <summary>
		///     Ambient
		/// </summary>
		Ambient = 3,

		/// <summary>
		///     Audio+Video
		/// </summary>
		AudioVideo = 4,

		/// <summary>
		///     UDP (Glimmr/WLED)
		/// </summary>
		Udp = 5,

		/// <summary>
		///     DreamScreen
		/// </summary>
		DreamScreen = 6
	}
}