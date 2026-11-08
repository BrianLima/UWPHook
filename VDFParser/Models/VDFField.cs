using System;
namespace VDFParser.Models {
    [AttributeUsage(AttributeTargets.Property)]
    public class VDFField : Attribute {
        public readonly string Name;
        public VDFFieldType Type { get; set; }

        public VDFField(string name) {
            Name = name;
            Type = VDFFieldType.String;
        }
    }
}