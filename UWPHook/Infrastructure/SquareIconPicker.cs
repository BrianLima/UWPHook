using System.Drawing;
using System.IO;
using UWPHook.Core.Artwork;
using UWPHook.Core.Diagnostics;

namespace UWPHook.Infrastructure
{
    /// <summary>Finds an app's icon among the logo images in its package folder (stub).</summary>
    internal sealed class SquareIconPicker(ILog log) : IIconPicker
    {
        public string? Pick(string folder) => null;
    }
}
