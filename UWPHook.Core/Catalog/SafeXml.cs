using System.Xml;
using System.Xml.Linq;

namespace UWPHook.Core.Catalog;

/// <summary>
/// Parses untrusted XML without throwing and without processing DTDs, and finds elements by local name
/// so that callers ignore which of the many Appx/GDK namespaces a file uses.
/// </summary>
internal static class SafeXml
{
    private static readonly XmlReaderSettings Settings = new()
    {
        DtdProcessing = DtdProcessing.Prohibit,
    };

    public static XElement? TryParseRoot(string xml, string expectedRootName)
    {
        if (string.IsNullOrWhiteSpace(xml))
            return null;

        try
        {
            using var reader = XmlReader.Create(new StringReader(xml), Settings);
            var root = XDocument.Load(reader).Root;
            return root?.Name.LocalName == expectedRootName ? root : null;
        }
        catch (XmlException)
        {
            return null;
        }
    }

    public static IEnumerable<XElement> Children(this IEnumerable<XElement> parents, string localName) =>
        parents.SelectMany(parent => parent.Elements().Where(child => child.Name.LocalName == localName));

    public static IEnumerable<XElement> Children(this XElement parent, string localName) =>
        new[] { parent }.Children(localName);

    public static string? AttributeValue(this XElement? element, string name) =>
        element?.Attribute(name)?.Value;
}
