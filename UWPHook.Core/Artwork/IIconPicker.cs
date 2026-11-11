namespace UWPHook.Core.Artwork;

/// <summary>
/// The one rule for an app's icon: the games list shows the file it picks, and an export gives the Steam shortcut
/// the same file, so the two never disagree.
/// </summary>
public interface IIconPicker
{
    /// <summary>The icon among the images in <paramref name="folder"/> (an app's logo folder), or null when there is none.</summary>
    string? Pick(string folder);
}
