using UWPHook.Core.Diagnostics;

namespace UWPHook.Core.Tests.Diagnostics;

/// <summary>Keeps every entry written to it, for tests that check what was logged.</summary>
public sealed class RecordingLog : ILog
{
    public sealed record Entry(LogLevel Level, Exception? Exception, string Template, IReadOnlyList<object?> Values);

    public List<Entry> Entries { get; } = [];

    public void Write(LogLevel level, Exception? exception, string template, params object?[] values) =>
        Entries.Add(new Entry(level, exception, template, values));

    public IEnumerable<Entry> At(LogLevel level) => Entries.Where(entry => entry.Level == level);
}
