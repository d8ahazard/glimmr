#region

using System.Globalization;

#endregion

namespace Glimmr.Models.Util {
	public static class StringUtil {
		public static string UppercaseFirst(string s) {
			if (string.IsNullOrEmpty(s)) {
				return string.Empty;
			}

			var a = s.ToCharArray();
			a[0] = char.ToUpper(a[0], CultureInfo.InvariantCulture);
			return new string(a);
		}
	}
}