using System.Collections.Generic;
using System.Text;

namespace VDFParser {
    public static class Extensions {

        /// <summary>
        /// Gets a string from this byte array
        /// </summary>
        /// <returns>A decoded string from the byte array</returns>
        public static string StringFromByteArray(this List<byte> b) {
            return b.ToArray().StringFromByteArray();
        }

        /// <summary>
        /// Gets a string from this byte array
        /// </summary>
        /// <returns>A decoded string from the byte array</returns>
        public static string StringFromByteArray(this byte[] list) {
            return new UTF8Encoding().GetString(list); 
        }
    }
}
