using System.Xml.Linq;

namespace UWPHook.Core.Catalog;

/// <summary>Reads the applications declared in an AppxManifest.xml.</summary>
public static class AppxManifest
{
    /// <summary>
    /// Returns one entry per <c>Package/Applications/Application</c> that has an <c>Id</c>, in document order.
    /// Elements are matched by local name, so any foundation/uap/uapN namespace works.
    /// Malformed XML yields an empty list; this method never throws on bad input.
    /// </summary>
    public static IReadOnlyList<ManifestApplication> ReadApplications(string xml)
    {
        var package = SafeXml.TryParseRoot(xml, "Package");
        if (package is null)
            return [];

        return package
            .Children("Applications")
            .Children("Application")
            .Select(ToManifestApplication)
            .OfType<ManifestApplication>()
            .ToList();
    }

    private static ManifestApplication? ToManifestApplication(XElement application)
    {
        var id = application.AttributeValue("Id");
        if (string.IsNullOrEmpty(id))
            return null;

        var visualElements = application.Children("VisualElements").FirstOrDefault();
        return new ManifestApplication(
            id,
            application.AttributeValue("Executable"),
            visualElements.AttributeValue("DisplayName"),
            visualElements.AttributeValue("Square150x150Logo"));
    }
}
