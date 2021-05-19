using System;
using System.Globalization;

namespace Glimmr.Models.Util {
	public static class ByteUtils {
		private static readonly IFormatProvider Format = new CultureInfo("en-US");

        /// <summary>
        ///     Convert an integer to a byte
        /// </summary>
        /// <returns>
        ///     A byte representation of the integer.
        /// </returns>
        public static byte IntByte(int toByte, string format = "X2") {
			var b = Convert.ToByte(toByte.ToString(format, Format), 16);
			return b;
		}
	}
}