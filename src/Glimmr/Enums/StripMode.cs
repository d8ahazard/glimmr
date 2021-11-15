namespace Glimmr.Enums {
	public enum StripMode {
		/// <summary>
		///     Normal.
		/// </summary>
		Normal = 0,

		/// <summary>
		///     Sectored (use WLED segments).
		/// </summary>
		Sectored = 1,

		/// <summary>
		///     Loop colors (strip is divided in half, second half of colors are mirrored).
		/// </summary>
		Loop = 2,

		/// <summary>
		///     All leds use a single sector.
		/// </summary>
		Single = 3
	}
}