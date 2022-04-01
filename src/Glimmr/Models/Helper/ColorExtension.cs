#region

using System.Drawing;

#endregion

namespace Glimmr.Models.Helper;

public static class ColorExtension {
	public static string ToHex(this Color c) {
		return "#" + c.R.ToString("X2") + c.G.ToString("X2") + c.B.ToString("X2");
	}
}