using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HueDream.Util {
    public static class ByteStringUtil {

        /// <summary>
        /// Convert an ASCII string and pad or truncate
        /// </summary>
        /// <returns>
        /// A byte array representing the padded/truncated string
        /// </returns>
        public static byte[] StringBytePad(string toPad, int len) {
            string output = "";
            if (toPad.Length > len) {
                output = toPad.Substring(0, len);
            } else {
                output = toPad;
            }
            System.Text.ASCIIEncoding encoding = new ASCIIEncoding();
            byte[] outBytes = new byte[len];
            byte[] myBytes = encoding.GetBytes(output);
            for (int bb = 0; bb < len; bb++) {
                if (bb < myBytes.Length) {
                    outBytes[bb] = myBytes[bb];
                } else {
                    outBytes[bb] = 0;
                }
            }
            return outBytes;
        }

        public static IEnumerable<string> SplitHex(string str, int chunkSize) {
            return Enumerable.Range(0, str.Length / chunkSize)
                .Select(i => str.Substring(i * chunkSize, chunkSize));
        }

        public static byte[] HexBytes(string input) {
            List<byte> output = new List<byte>();
            foreach (string hx in SplitHex(input, 2)) {
                output.Add(Convert.ToByte(hx, 16));
            }
            return output.ToArray();
        }

        /// <summary>
        /// Convert an integer to a byte
        /// </summary>
        /// <returns>
        /// A byte representation of the integer.
        /// </returns>
        public static byte IntByte(int toByte) {
            byte b = Convert.ToByte(toByte.ToString("X2"), 16);
            return b;
        }

        /// <summary>
        /// Convert an Hex string to it's integer representation
        /// </summary>
        /// <returns>
        /// The integer version of the hex string
        /// </returns>
        public static int HexInt(string intIn) {
            return int.Parse(intIn, System.Globalization.NumberStyles.HexNumber);
        }

        /// <summary>
        /// Convert a single hex string to it's byte representation
        /// </summary>
        /// <returns>
        /// A byte representing that value
        /// </returns>
        public static byte HexByte(string hexStr) {
            return Convert.ToByte(hexStr, 16);
        }

        /// <summary>
        /// Convert an arbitrary length String of hex characters with no spacing into it's ASCII representation
        /// </summary>
        /// <returns>
        /// An ASCII representation of the hex string
        /// </returns>
        public static string HexString(string hexString) {
            string sb = "";
            for (int i = 0; i < hexString.Length; i += 2) {
                string hs = hexString.Substring(i, 2);
                sb += HexChar(hs);
            }
            return sb;
        }

        /// <summary>
        /// Extract a hex string from a larger string from a known start and end position
        /// </summary>
        /// <returns>
        /// A string of hex-encoded values with no spacing
        /// </returns>
        public static string ExtractHexString(string[] input, int start, int len) {
            string strOut = "";
            if (len < input.Length) {
                string[] nameArr = new string[len];
                Array.Copy(input, start, nameArr, 0, len);

                foreach (string s in nameArr) {
                    strOut += HexChar(s);
                }
            } else {
                Console.WriteLine("Len for input request " + len + " is less than array len: " + input.Length);
            }
            return strOut;
        }

        /// <summary>
        /// Um... Convert a hex string to ASCII. I think I need to figure out why I'm calling this from another method
        /// </summary>
        /// <returns>
        /// A byte array representing the padded/truncated string
        /// </returns>
        public static string HexChar(string hexString) {
            try {
                string ascii = string.Empty;

                for (int i = 0; i < hexString.Length; i += 2) {
                    string hs = string.Empty;

                    hs = hexString.Substring(i, 2);
                    uint decval = Convert.ToUInt32(hs, 16);
                    char character = Convert.ToChar(decval);
                    ascii += character;

                }

                return ascii;
            } catch (Exception ex) { Console.WriteLine(ex.Message); }

            return string.Empty;
        }
    }
}

