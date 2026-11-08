using System.Collections.Generic;
namespace VDFParser.Machines {

    /// <summary>
    /// State Machine used to parse an indexed array from the VDF structure
    /// </summary>
    public class VDFIndexedArraySM {
        enum State {
            IndexIdentifier,
            Index,
            Value
        }
        State state;
        readonly List<string> result;
        readonly List<byte> tmpBuffer;

        /// <summary>
        /// Gets the parsed array.
        /// </summary>
        /// <value>The parsed array.</value>
        public string[] ParsedArray {
            get {
                return result.ToArray();
            }
        }

        /// <summary>
        /// Resets this instance, cleaning all parsed results
        /// </summary>
        public void Reset() {
            result.Clear();
            tmpBuffer.Clear();
            state = State.IndexIdentifier;
        }

        /// <summary>
        /// Flushes any remaining field to the result array
        /// </summary>
        public void Flush() {
            if(tmpBuffer.Count > 0) {
                result.Add(tmpBuffer.StringFromByteArray());
            }
        }
            
        /// <summary>
        /// Initializes a new instance of the <see cref="T:VDFParser.Machines.VDFIndexedArraySM"/> class.
        /// </summary>
        public VDFIndexedArraySM() {
            result = new List<string>();
            tmpBuffer = new List<byte>();
        }

        /// <summary>
        /// Feeds a given byte to the SM
        /// </summary>
        /// <param name="b">Incoming byte to be fed to the SM</param>
        public void Feed(byte b) {
            switch(state) {
                case State.IndexIdentifier:
                    if(b == 0x01) {
                        state = State.Index;
                        Flush();
                        tmpBuffer.Clear();
                    }
                    break;
                case State.Index:
                    if(b == 0x00) {
                        // Look! Is this an index metadata we're getting rid of?
                        tmpBuffer.Clear();
                        state = State.Value;
                    }
                    // tmpBuffer.Add(b); 
                    break;
                case State.Value:
                    if(b == 0x00) {
                        result.Add(tmpBuffer.StringFromByteArray());
                        tmpBuffer.Clear();
                        state = State.IndexIdentifier;
                        break;
                    }
                    tmpBuffer.Add(b);
                    break;
            }
        }

        /// <summary>
        /// Feeds a given byte array to the SM
        /// </summary>
        /// <param name="b">Incoming byte array to be fed to the SM</param>
        public void Feed(List<byte> b) { Feed(b.ToArray()); }

        /// <summary>
        /// Feeds a given byte array to the SM
        /// </summary>
        /// <param name="b">Incoming byte array to be fed to the SM</param>
        public void Feed(byte[] input) {
            foreach(var b in input) {
                Feed(b);
            }
        }
    }
}
