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

    [Fact]
    public void SteamStillRunning_TellsTheUserToCloseSteamAndRetry()
    {
        var summary = ExportSummary.SteamStillRunning();

        Assert.Equal(ExportStatus.SteamStillRunning, summary.Status);
        Assert.Equal(MessageSeverity.Warning, summary.Severity);
        Assert.Contains("nothing was exported", summary.Message);
        Assert.Contains("Close Steam", summary.Message);
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

    [Theory]
    [InlineData(ExportStatus.NothingSelected, false)]
    [InlineData(ExportStatus.SteamNotFound, false)]
    [InlineData(ExportStatus.NoSteamUsers, false)]
    [InlineData(ExportStatus.SteamStillRunning, false)]
    [InlineData(ExportStatus.SteamPathUnknown, false)]
    [InlineData(ExportStatus.Failed, false)]
    [InlineData(ExportStatus.PartiallyExported, true)]
    [InlineData(ExportStatus.Exported, true)]
    public void WroteAnything_OnlyWhenAtLeastOneUserWasWritten(ExportStatus status, bool expected)
    {
        var summary = status switch
        {
            ExportStatus.NothingSelected => ExportSummary.NothingSelected(),
            ExportStatus.SteamNotFound => ExportSummary.SteamNotFound(),
            ExportStatus.SteamStillRunning => ExportSummary.SteamStillRunning(),
            ExportStatus.SteamPathUnknown => ExportSummary.SteamNotStopped(StopOutcome.ExecutableUnknown),
            ExportStatus.NoSteamUsers => ExportSummary.ForUsers(0, 0, StopOutcome.NotRunning),
            ExportStatus.Failed => ExportSummary.ForUsers(0, 1, StopOutcome.NotRunning),
            ExportStatus.PartiallyExported => ExportSummary.ForUsers(1, 1, StopOutcome.NotRunning),
            _ => ExportSummary.ForUsers(1, 0, StopOutcome.NotRunning),
        };

        Assert.Equal(status, summary.Status);
        Assert.Equal(expected, summary.WroteAnything);
    }
}
