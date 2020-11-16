using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

namespace Glimmr.Models.Util {
    public static class ByteUtils {
        private static readonly IFormatProvider Format = new CultureInfo("en-US");

        /// <summary>
        ///     Convert an ASCII string and pad or truncate
        /// </summary>
        /// <returns>
        ///     A byte array representing the padded/truncated string
        /// </returns>
        public static IEnumerable<byte> StringBytePad(string toPad, int len) {
            if (toPad is null) throw new ArgumentNullException(nameof(toPad));

            var outBytes = new byte[len];
            var output = toPad.Length > len ? toPad.Substring(0, len) : toPad;
            var encoding = new ASCIIEncoding();

            var myBytes = encoding.GetBytes(output);
            for (var bb = 0; bb < len; bb++)
                if (bb < myBytes.Length)
                    outBytes[bb] = myBytes[bb];
                else
                    outBytes[bb] = 0;

            return outBytes;
        }

        public static byte[] StringBytes(string hexString) {
            if (hexString is null) throw new ArgumentNullException(nameof(hexString));

            if (hexString.Length % 2 != 0)
                throw new ArgumentException(string.Format(CultureInfo.InvariantCulture,
                    "The binary key cannot have an odd number of digits: {0}", hexString));

            var data = new byte[hexString.Length / 2];
            for (var index = 0; index < data.Length; index++) {
                var byteValue = hexString.Substring(index * 2, 2);
                data[index] = byte.Parse(byteValue, NumberStyles.HexNumber, CultureInfo.InvariantCulture);
            }

            return data;
        }

        public static List<string> SplitHex(string value, int chunkLength) {
            if (value == null) throw new ArgumentException("Invalid input."); 
            var res = new List<string>();
            var count = value.Length / chunkLength + (value.Length % chunkLength > 0 ? 1 : 0);
            Enumerable.Range(0, count).ToList().ForEach(f =>
                res.Add(value.Skip(f * chunkLength).Take(chunkLength).Select(z => z.ToString(CultureInfo.InvariantCulture))
                    .Aggregate((a, b) => a + b)));
            return res;
        }

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

        // Convert an array of integers to bytes
        public static byte[] IntBytes(int[] toBytes) {
            //if (toBytes == null) throw new ArgumentException("Invalid input.");
            var output = new byte[toBytes.Length];
            var c = 0;
            foreach (var i in toBytes) {
                output[c] = Convert.ToByte(i.ToString("X2", Format), 16);
                c++;
            }
            return output;
        }

        /// <summary>
        ///     Extract a hex string from a larger string from a known start and end position
        /// </summary>
        /// <returns>
        ///     A string of hex-encoded values with no spacing
        /// </returns>
        public static string ExtractString(byte[] input, int start, int end, bool hex=false) {
            if (input is null) throw new ArgumentNullException(nameof(input));
            var len = end - start;
            var strOut = "";
            if (len < input.Length) {
                var subArr = new byte[len];
                Array.Copy(input, start, subArr, 0, len);
                strOut = hex ? BitConverter.ToString(subArr).Replace("-","", StringComparison.InvariantCulture) : subArr.Aggregate(strOut, (current, b) => current + Convert.ToChar(b));
            } else {
                throw new IndexOutOfRangeException();
            }

            return strOut;
        }

        public static int[] ExtractInt(byte[] input, int start, int end) {
            if (input == null) throw new ArgumentException("Invalid input.");
            var len = end - start;
            var intOut = new int[len];
            if (len < input.Length) {
                var subArr = new byte[len];
                Array.Copy(input, start, subArr, 0, len);
                var c = 0;

                foreach (var b in subArr) {
                    intOut[c] = b;
                    c++;
                }
            } else {
                throw new IndexOutOfRangeException();
            }

            return intOut;
        }

        public static byte[] ExtractBytes(byte[] input, int start, int end) {
            if (input == null) throw new ArgumentException("Invalid input.");
            var len = end - start;
            var byteOut = new byte[len];
            if (len < input.Length) {
                var subArr = new byte[len];
                Array.Copy(input, start, subArr, 0, len);
            } else {
                throw new IndexOutOfRangeException();
            }

            return byteOut;
        }

        public static byte[] PadInt(int toPad, int take = 2) {
            byte[] intBytes = BitConverter.GetBytes(toPad);
            Array.Reverse(intBytes);
            intBytes = intBytes.Reverse().Take(take).Reverse().ToArray();
            return intBytes;
        }

        public static string ByteString(byte[] input) {
            if (input is null) throw new ArgumentNullException(nameof(input));

            var strOut = "";
            foreach (var b in input) strOut += b.ToString("X2", Format);
            return strOut;
        }
    }
}