namespace UWPHook.Core.Platform;

/// <summary>The version of the Windows UWPHook runs on (Windows 11 reports 10.0 with a build of 22000 or later).</summary>
public interface IOsVersion
{
    int Major { get; }

    int Minor { get; }

    int Build { get; }
}
