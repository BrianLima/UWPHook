using Serilog.Events;
using UWPHook.Core.Diagnostics;

namespace UWPHook.Infrastructure
{
    /// <summary>Forwards what Core logs to the app's Serilog logger (application.log).</summary>
    internal sealed class SerilogLog : ILog
    {
        public static SerilogLog Instance { get; } = new SerilogLog();

        private SerilogLog()
        {
        }

        public void Write(LogLevel level, Exception? exception, string template, params object?[] values) =>
            Serilog.Log.Write(ToSerilog(level), exception, template, values);

        private static LogEventLevel ToSerilog(LogLevel level) => level switch
        {
            LogLevel.Debug => LogEventLevel.Debug,
            LogLevel.Information => LogEventLevel.Information,
            LogLevel.Warning => LogEventLevel.Warning,
            _ => LogEventLevel.Error,
        };
    }
}
