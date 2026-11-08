namespace UWPHook.Core.Diagnostics;

public enum LogLevel
{
    Debug,
    Information,
    Warning,
    Error,
}

/// <summary>
/// Where Core reports what it did. The app forwards it to Serilog, so <c>template</c> is a Serilog
/// message template ("Saving {Path} failed") and <c>values</c> fill its holes in order.
/// </summary>
public interface ILog
{
    void Write(LogLevel level, Exception? exception, string template, params object?[] values);
}

/// <summary>Writes nothing: for callers that don't need a log.</summary>
public sealed class NullLog : ILog
{
    public static NullLog Instance { get; } = new();

    private NullLog()
    {
    }

    public void Write(LogLevel level, Exception? exception, string template, params object?[] values)
    {
    }
}

public static class LogExtensions
{
    public static void Debug(this ILog log, string template, params object?[] values) =>
        log.Write(LogLevel.Debug, null, template, values);

    public static void Information(this ILog log, string template, params object?[] values) =>
        log.Write(LogLevel.Information, null, template, values);

    public static void Warning(this ILog log, string template, params object?[] values) =>
        log.Write(LogLevel.Warning, null, template, values);

    public static void Warning(this ILog log, Exception exception, string template, params object?[] values) =>
        log.Write(LogLevel.Warning, exception, template, values);

    public static void Error(this ILog log, Exception exception, string template, params object?[] values) =>
        log.Write(LogLevel.Error, exception, template, values);
}
