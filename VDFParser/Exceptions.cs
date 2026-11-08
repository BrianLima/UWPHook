using System;
namespace VDFParser {

    /// <summary>
    /// Indicates that a VDF file is too short (either it does not contain any
    /// substantial data, or is corrupted)
    /// </summary>
    public class VDFTooShortException : Exception { 
        public VDFTooShortException(string message) : base(message) { }
    }
}
