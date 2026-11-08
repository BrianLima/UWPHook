using System.IO;
using System.Text;
using System;

namespace VDFParser {

    /// <summary>
    /// Implements a generic writer utility for writing a VDF file
    /// </summary>
    public class GenericWriter {
        Stream s;

        /// <summary>
        /// Initializes a new instance of the <see cref="T:VDFParser.GenericWriter"/> class.
        /// </summary>
        /// <param name="s">Stream to be written to</param>
        public GenericWriter(Stream s) {
            this.s = s;
        }

        /// <summary>
        /// Writes a nil byte to the stream
        /// </summary>
        public void Nil() {
            s.WriteByte(0x00);
        }

        /// <summary>
        /// Writes a given value to the stream
        /// </summary>
        /// <param name="value">String value to be written</param>
        public void Write(string value) {
            Write(Encoding.UTF8.GetBytes(value));
        }

        /// <summary>
        /// Writes a given value to the stream
        /// </summary>
        /// <param name="value">Byte array to be written</param>
        public void Write(byte[] value) {
            s.Write(value, 0, value.Length);
        }

        /// <summary>
        /// Writes a given value to the stream
        /// </summary>
        /// <param name="value">Integer to be written</param>
        public void Write(int value) {
            Write(BitConverter.GetBytes(value));
        }

        /// <summary>
        /// Writes a given value to the stream
        /// </summary>
        /// <param name="value">Single byte to be written</param>
        public void Write(byte value) {
            s.WriteByte(value);
        }
    }
}
