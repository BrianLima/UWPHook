using VDFParser.Machines;
using VDFParser.Models;
using System.IO;
using System.Linq;

namespace VDFParser {

    /// <summary>
    /// Implements a specialized parser for VDF files
    /// </summary>
    public static class VDFParser {

        /// <summary>
        /// Parses a given VDF file into an array of <see cref="T:VDFParser.Models.VDFEntry"/> class
        /// Throws a <see cref="T:System.IO.FileNotFoundException"/> if the file does not exist.
        /// Throws a <see cref="T:VDFParser.VDFTooShortException"/> if the file is too short or does not contain substantial information.
        /// Throws an <see cref="T:System.IO.InvalidDataException"/> if the target file does not contain a valid header.
        /// </summary>
        /// <param name="path">Path to the file to be parsed.</param>
        public static VDFEntry[] Parse(string path) {
            if(!File.Exists(path)) {
                throw new FileNotFoundException(path);
            }
            using(FileStream stream = new FileStream(path, FileMode.Open)) {
                return Parse(stream);
            }
        }


        /// <summary>
        /// Parses a given VDF file into an array of <see cref="T:VDFParser.Models.VDFEntry"/> class
        /// Throws a <see cref="T:VDFParser.VDFTooShortException"/> if the file is too short or does not contain substantial information.
        /// Throws an <see cref="T:System.IO.InvalidDataException"/> if the target file does not contain a valid header.
        /// </summary>
        /// <param name="stream">Stream to be parsed.</param>
        public static VDFEntry[] Parse(Stream stream) {
            if(stream.Length < 16) {
                throw new VDFTooShortException("VDF is too short and probably does not contain any substantial information.");
            }
            byte[] headerBuffer = new byte[11];
            stream.Read(headerBuffer, 0, 11);


            if(!headerBuffer.SequenceEqual(Shared.VDFHeader)) {
                throw new InvalidDataException("Invalid header detected. Cannot continue.");
            }

            byte[] buffer = new byte[1024];
            int bufferLen;

            var sm = new VDFSM();
            while((bufferLen = stream.Read(buffer, 0, buffer.Length)) > 0) {
                for(var i = 0; i < bufferLen; i++) {
                    sm.Feed(buffer[i]);
                }
            }
            sm.Flush();
            return sm.Entries;
        }
    }
}
