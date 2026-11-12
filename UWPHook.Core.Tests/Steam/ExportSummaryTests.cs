using UWPHook.Core.Steam;
using Xunit;

namespace UWPHook.Core.Tests.Steam;

public class ExportSummaryTests
{
    [Fact]
    public void NothingSelected_AsksToSelectAnApp()
    {
        var summary = ExportSummary.NothingSelected();

        Assert.Equal(ExportStatus.NothingSelected, summary.Status);
        Assert.Equal(MessageSeverity.Information, summary.Severity);
        Assert.Contains("Select at least one app", summary.Message);
    }

    [Fact]
    public void SteamNotFound_IsAnError()
    {
        var summary = ExportSummary.SteamNotFound();

        Assert.Equal(ExportStatus.SteamNotFound, summary.Status);
        Assert.Equal(MessageSeverity.Error, summary.Severity);
        Assert.Contains("Steam was not found", summary.Message);
        Assert.DoesNotContain("success", summary.Message, StringComparison.OrdinalIgnoreCase);
    }

    // Steam was asked to exit and usually still does, a little later: the user is not told to close it, and is told
    // that nothing starts it again.
    [Fact]
    public void SteamStillRunning_SaysSteamMayStillCloseAndIsNotStartedAgain()
    {
        var summary = ExportSummary.SteamStillRunning();

        Assert.Equal(ExportStatus.SteamStillRunning, summary.Status);
        Assert.Equal(MessageSeverity.Warning, summary.Severity);
        Assert.Equal(
            "Steam is still closing, so nothing was exported. Wait until it has closed, then export again. UWPHook will not start Steam again after this, so start it yourself.",
            summary.Message);
    }

    [Fact]
    public void ForUsers_WhenSteamCouldNotBeStartedAgain_SaysToStartIt_AsAWarning()
    {
        var summary = ExportSummary.ForUsers(written: 2, failed: 0, StopOutcome.NotStartedAgain);

        Assert.Equal(ExportStatus.Exported, summary.Status);
        Assert.Equal(MessageSeverity.Warning, summary.Severity);
        Assert.Equal("Your apps were successfully exported! Steam could not be started again; start it yourself.", summary.Message);
    }

    [Fact]
    public void ForUsers_WhenEveryUserFailed_AndSteamCouldNotBeStartedAgain_SaysToStartIt()
    {
        var summary = ExportSummary.ForUsers(written: 0, failed: 1, StopOutcome.NotStartedAgain);

        Assert.Equal(ExportStatus.Failed, summary.Status);
        Assert.EndsWith(" Steam could not be started again; start it yourself.", summary.Message);
    }

    [Fact]
    public void ForUsers_WhenEveryUserFailed_AndSteamWasRestarted_SaysSo()
    {
        var summary = ExportSummary.ForUsers(written: 0, failed: 2, StopOutcome.Stopped);

        Assert.Equal(ExportStatus.Failed, summary.Status);
        Assert.Equal(MessageSeverity.Error, summary.Severity);
        Assert.StartsWith("Your apps were not exported: it failed for all 2 Steam account(s).", summary.Message);
        Assert.EndsWith(" Steam has been restarted.", summary.Message);
    }

    [Fact]
    public void SteamPathUnknown_DoesNotClaimSteamWasAskedToClose()
    {
        var summary = ExportSummary.SteamNotStopped(StopOutcome.ExecutableUnknown);

        Assert.Equal(ExportStatus.SteamPathUnknown, summary.Status);
        Assert.Equal(MessageSeverity.Warning, summary.Severity);
        Assert.DoesNotContain("did not close in time", summary.Message);
        Assert.Contains("steam.exe", summary.Message);
        Assert.Contains("nothing was exported", summary.Message);
        Assert.Contains("Close Steam", summary.Message);
    }

    // Manual-test finding 3: the user chose to keep Steam open, so this is information, not a warning.
    [Fact]
    public void SteamNotStopped_WhenTheUserKeptSteamOpen_SaysNothingWasExported()
    {
        var summary = ExportSummary.SteamNotStopped(StopOutcome.Declined);

        Assert.Equal((ExportStatus.SteamLeftOpen, StopOutcome.Declined), (summary.Status, summary.Steam));
        Assert.Equal(MessageSeverity.Information, summary.Severity);
        Assert.Equal("Nothing was exported. Close Steam and export again.", summary.Message);
    }

    [Fact]
    public void SteamNotStopped_AfterATimeout_IsSteamStillRunning() =>
        Assert.Equal(ExportStatus.SteamStillRunning, ExportSummary.SteamNotStopped(StopOutcome.StillRunning).Status);

    [Fact]
    public void ForUsers_WhenSteamsPathWasUnknown_IsSteamPathUnknown() =>
        Assert.Equal(ExportStatus.SteamPathUnknown, ExportSummary.ForUsers(0, 0, StopOutcome.ExecutableUnknown).Status);

    [Fact]
    public void WithArtworkFailures_AddsANoteNamingTheUsers_AndKeepsTheStatus()
    {
        var summary = ExportSummary.ForUsers(written: 2, failed: 0, StopOutcome.Stopped).WithArtworkFailures(["123", "456"]);

        Assert.Equal(ExportStatus.Exported, summary.Status);
        Assert.Equal(MessageSeverity.Warning, summary.Severity);
        Assert.StartsWith("Your apps were successfully exported!", summary.Message);
        Assert.Contains("Artwork could not be copied for Steam account(s) 123, 456", summary.Message);
        Assert.Contains("Steam has been restarted.", summary.Message);
    }

    // The shortcuts are in Steam, but not in the collections the setting names: the user is told, the export stands.
    [Fact]
    public void WithCollectionsSkipped_AddsANoteNamingTheUsers_AndKeepsTheStatus()
    {
        var summary = ExportSummary.ForUsers(written: 2, failed: 0, StopOutcome.Stopped).WithCollectionsSkipped(["123", "456"]);

        Assert.Equal(ExportStatus.Exported, summary.Status);
        Assert.Equal(MessageSeverity.Warning, summary.Severity);
        Assert.StartsWith("Your apps were successfully exported!", summary.Message);
        Assert.Contains("They could not be added to your Steam collections for Steam account(s) 123, 456; details are in application.log.", summary.Message);
        Assert.EndsWith("Steam has been restarted.", summary.Message);
    }

    // An account without a collections file (an old account folder) is common: said, but not as a warning.
    [Fact]
    public void WithoutCollections_AddsANoteNamingTheUsers_AsInformation()
    {
        var summary = ExportSummary.ForUsers(written: 2, failed: 0, StopOutcome.Stopped).WithoutCollections(["456"]);

        Assert.Equal(ExportStatus.Exported, summary.Status);
        Assert.Equal(MessageSeverity.Information, summary.Severity);
        Assert.StartsWith("Your apps were successfully exported!", summary.Message);
        Assert.Contains("Steam account(s) 456 have no Steam collections yet, so the games were not added to collections there.", summary.Message);
        Assert.EndsWith("Steam has been restarted.", summary.Message);
    }

    [Fact]
    public void WithoutCollections_WithNone_ChangesNothing()
    {
        var summary = ExportSummary.ForUsers(written: 1, failed: 0, StopOutcome.NotRunning);

        Assert.Equal(summary.Message, summary.WithoutCollections([]).Message);
    }

    [Fact]
    public void WithCollectionsSkipped_WithNoneSkipped_ChangesNothing()
    {
        var summary = ExportSummary.ForUsers(written: 1, failed: 0, StopOutcome.NotRunning);

        Assert.Equal(summary.Message, summary.WithCollectionsSkipped([]).Message);
        Assert.Equal(MessageSeverity.Information, summary.WithCollectionsSkipped([]).Severity);
    }

    // Review m3: an app whose artwork could not be downloaded is a note too (R33: artwork is optional).
    [Fact]
    public void WithArtworkDownloadFailures_AddsANoteNamingTheApps_AndKeepsTheStatus()
    {
        var summary = ExportSummary.ForUsers(written: 1, failed: 0, StopOutcome.NotRunning).WithArtworkDownloadFailures(["Halo", "Forza"]);

        Assert.Equal(ExportStatus.Exported, summary.Status);
        Assert.Equal(MessageSeverity.Warning, summary.Severity);
        Assert.StartsWith("Your apps were successfully exported!", summary.Message);
        Assert.Contains("Artwork could not be downloaded for Halo, Forza; details are in application.log.", summary.Message);
    }

    [Fact]
    public void WithArtworkDownloadFailures_WithNoFailures_ChangesNothing()
    {
        var summary = ExportSummary.ForUsers(written: 1, failed: 0, StopOutcome.NotRunning);

        Assert.Equal(summary.Message, summary.WithArtworkDownloadFailures([]).Message);
        Assert.Equal(MessageSeverity.Information, summary.WithArtworkDownloadFailures([]).Severity);
    }

    [Fact]
    public void WithArtworkFailures_WithNoFailures_ChangesNothing()
    {
        var summary = ExportSummary.ForUsers(written: 1, failed: 0, StopOutcome.NotRunning);

        Assert.Equal(summary.Message, summary.WithArtworkFailures([]).Message);
        Assert.Equal(MessageSeverity.Information, summary.WithArtworkFailures([]).Severity);
    }

    [Fact]
    public void ForUsers_WithNoUsers_SaysNoSteamAccountWasFound()
    {
        var summary = ExportSummary.ForUsers(written: 0, failed: 0, StopOutcome.NotRunning);

        Assert.Equal(ExportStatus.NoSteamUsers, summary.Status);
        Assert.Equal(MessageSeverity.Error, summary.Severity);
        Assert.Contains("No Steam account", summary.Message);
    }

    [Fact]
    public void ForUsers_WhenEveryUserFailed_IsAnErrorNamingTheCount()
    {
        var summary = ExportSummary.ForUsers(written: 0, failed: 2, StopOutcome.NotRunning);

        Assert.Equal(ExportStatus.Failed, summary.Status);
        Assert.Equal(MessageSeverity.Error, summary.Severity);
        Assert.Contains("2", summary.Message);
        Assert.Contains("application.log", summary.Message);
        Assert.DoesNotContain("success", summary.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ForUsers_WhenSomeUsersFailed_IsAPartialSuccessNamingTheCounts()
    {
        var summary = ExportSummary.ForUsers(written: 1, failed: 2, StopOutcome.NotRunning);

        Assert.Equal(ExportStatus.PartiallyExported, summary.Status);
        Assert.Equal(MessageSeverity.Warning, summary.Severity);
        Assert.Contains("1 of 3", summary.Message);
        Assert.Contains("2 failed", summary.Message);
        Assert.Contains("application.log", summary.Message);
    }

    [Fact]
    public void ForUsers_WhenEveryUserWasWritten_AndSteamWasRestarted_SaysSo()
    {
        var summary = ExportSummary.ForUsers(written: 2, failed: 0, StopOutcome.Stopped);

        Assert.Equal(ExportStatus.Exported, summary.Status);
        Assert.Equal(MessageSeverity.Information, summary.Severity);
        Assert.Equal("Your apps were successfully exported! Steam has been restarted.", summary.Message);
    }

    [Fact]
    public void ForUsers_WhenEveryUserWasWritten_AndSteamWasNotRunning_SaysWhenTheyShowUp()
    {
        var summary = ExportSummary.ForUsers(written: 1, failed: 0, StopOutcome.NotRunning);

        Assert.Equal("Your apps were successfully exported! They will show up the next time you start Steam.", summary.Message);
    }

    [Fact]
    public void ForUsers_WhenSteamCouldNotBeStopped_IsSteamStillRunning()
    {
        // Nothing is written while Steam runs, whatever the counts say.
        Assert.Equal(ExportStatus.SteamStillRunning, ExportSummary.ForUsers(written: 0, failed: 0, StopOutcome.StillRunning).Status);
    }

    // Fix-wave minor 5: a named factory instead of ForUsers(0, 0, NotRunning).
    [Fact]
    public void NoSteamUsers_SaysNoSteamAccountWasFound()
    {
        var summary = ExportSummary.NoSteamUsers();

        Assert.Equal(ExportStatus.NoSteamUsers, summary.Status);
        Assert.Equal(MessageSeverity.Error, summary.Severity);
        Assert.Equal(ExportSummary.ForUsers(0, 0, StopOutcome.NotRunning).Message, summary.Message);
    }
}
