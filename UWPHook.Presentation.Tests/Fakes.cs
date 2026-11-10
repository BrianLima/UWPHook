using UWPHook.Core.Catalog;
using UWPHook.Core.Configuration;
using UWPHook.Core.Diagnostics;
using UWPHook.Core.Steam;

namespace UWPHook.Presentation.Tests;

/// <summary>Records what the user was shown and answers confirmations with <see cref="Answer"/>.</summary>
public sealed class FakeDialogs : IDialogService
{
    public sealed record Shown(MessageSeverity Severity, string Message, string Title);

    public List<Shown> Messages { get; } = [];
    public List<(string Message, ConfirmTone Tone)> Questions { get; } = [];
    public List<(Exception Exception, string UserMessage)> Errors { get; } = [];
    public bool Answer { get; set; } = true;

    public void Show(MessageSeverity severity, string message, string title = "UWPHook") => Messages.Add(new(severity, message, title));

    public bool Confirm(string message, ConfirmTone tone)
    {
        Questions.Add((message, tone));
        return Answer;
    }

    public void ReportError(Exception exception, string userMessage) => Errors.Add((exception, userMessage));
}

/// <summary>Settings in memory; set <see cref="SaveFailure"/> to make saving throw (and keep the old settings).</summary>
public sealed class FakeSettings(AppSettings current) : ISettingsService
{
    public AppSettings Current { get; private set; } = current;
    public Exception? SaveFailure { get; set; }
    public int Saves { get; private set; }

    public void Update(Func<AppSettings, AppSettings> change)
    {
        var changed = change(Current);
        if (SaveFailure is not null) throw SaveFailure;
        Saves++;
        Current = changed;
    }
}

/// <summary>A catalog that answers with <see cref="Apps"/>, or waits for <see cref="Gate"/> when it is set.</summary>
public sealed class FakeCatalog(params XboxApp[] apps) : IAppCatalog
{
    public IReadOnlyList<XboxApp> Apps { get; set; } = apps;
    public List<bool> IncludeSystemAsked { get; } = [];
    public Exception? Failure { get; set; }
    public TaskCompletionSource? Gate { get; set; }

    public async Task<IReadOnlyList<XboxApp>> GetInstalledAppsAsync(bool includeSystem, CancellationToken ct)
    {
        IncludeSystemAsked.Add(includeSystem);
        if (Gate is not null) await Gate.Task;
        if (Failure is not null) throw Failure;
        return Apps;
    }
}

/// <summary>Answers with the summary (or throws <see cref="Failure"/>), after <see cref="Gate"/> when it is set.</summary>
public sealed class FakeExporter(ExportSummary summary) : IShortcutExporter
{
    public List<ExportRequest> Requests { get; } = [];
    public Exception? Failure { get; set; }
    public TaskCompletionSource? Gate { get; set; }

    public async Task<ExportSummary> ExportAsync(ExportRequest request, CancellationToken ct = default)
    {
        Requests.Add(request);
        if (Gate is not null) await Gate.Task;
        if (Failure is not null) throw Failure;
        return summary;
    }
}

/// <summary>Answers with the summary (or throws <see cref="Failure"/>), after <see cref="Gate"/> when it is set.</summary>
public sealed class FakeRemover(RemoveSummary summary) : IShortcutRemover
{
    public List<SteamAccountChoice> Calls { get; } = [];

    /// <summary>The "may UWPHook close Steam?" question of each call, for a test to ask.</summary>
    public List<Func<bool>> MayCloseSteam { get; } = [];
    public Exception? Failure { get; set; }
    public TaskCompletionSource? Gate { get; set; }

    public async Task<RemoveSummary> RemoveAsync(SteamAccountChoice accounts, Func<bool> mayCloseSteam, CancellationToken ct = default)
    {
        Calls.Add(accounts);
        MayCloseSteam.Add(mayCloseSteam);
        if (Gate is not null) await Gate.Task;
        if (Failure is not null) throw Failure;
        return summary;
    }
}

public sealed class FakeSteam(string? installFolder, params SteamUser[] users) : ISteamLocator
{
    public string? InstallFolder() => installFolder;

    public string? SteamExecutable() => null;

    public IReadOnlyList<SteamUser> Users(string folder) => users;
}

public sealed class FakeHost : IAppHost
{
    public string Version => "2.15.0.0";
    public List<string> OpenedUrls { get; } = [];
    public List<AppLogLevel> AppliedLevels { get; } = [];

    public void OpenUrl(string url) => OpenedUrls.Add(url);

    public void ApplyLogLevel(AppLogLevel level) => AppliedLevels.Add(level);
}

public sealed class RecordingLog : ILog
{
    public List<(LogLevel Level, Exception? Exception, string Template)> Entries { get; } = [];

    public void Write(LogLevel level, Exception? exception, string template, params object?[] values) =>
        Entries.Add((level, exception, template));
}
