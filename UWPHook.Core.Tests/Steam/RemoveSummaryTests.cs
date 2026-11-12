using UWPHook.Core.Steam;
using Xunit;

namespace UWPHook.Core.Tests.Steam;

// The wording of "Remove UWPHook shortcuts", which used to be built in GamesWindow (fix-wave minor 6).
public class RemoveSummaryTests
{
    [Fact]
    public void SteamNotFound_IsAnError()
    {
        var summary = RemoveSummary.SteamNotFound();

        Assert.Equal(RemoveStatus.SteamNotFound, summary.Status);
        Assert.Equal(MessageSeverity.Error, summary.Severity);
        Assert.Equal("Steam was not found, so nothing was changed.", summary.Message);
    }

    [Fact]
    public void NothingFound_IsInformation()
    {
        var summary = RemoveSummary.NothingFound(unreadable: 0);

        Assert.Equal(RemoveStatus.NothingFound, summary.Status);
        Assert.Equal(MessageSeverity.Information, summary.Severity);
        Assert.Equal("No UWPHook shortcuts were found, so nothing was changed.", summary.Message);
    }

    [Fact]
    public void NothingFound_WithUnreadableAccounts_IsAWarningNamingTheCount()
    {
        var summary = RemoveSummary.NothingFound(unreadable: 2);

        Assert.Equal(MessageSeverity.Warning, summary.Severity);
        Assert.Equal(
            "No UWPHook shortcuts were found, so nothing was changed. The shortcuts of 2 Steam account(s) could not be read; details are in application.log.",
            summary.Message);
    }

    // Manual-test finding 3: the user chose to keep Steam open, so this is information, not a warning.
    [Fact]
    public void SteamNotStopped_WhenTheUserKeptSteamOpen_SaysNothingWasRemoved()
    {
        var summary = RemoveSummary.SteamNotStopped(StopOutcome.Declined);

        Assert.Equal((RemoveStatus.SteamLeftOpen, StopOutcome.Declined), (summary.Status, summary.Steam));
        Assert.Equal(MessageSeverity.Information, summary.Severity);
        Assert.Equal("Nothing was removed. Close Steam and try again.", summary.Message);
    }

    // Steam was asked to exit and usually still does, a little later: the user is not told to close it, and is told
    // that nothing starts it again.
    [Fact]
    public void SteamNotStopped_AfterATimeout_SaysSteamMayStillCloseAndIsNotStartedAgain()
    {
        var summary = RemoveSummary.SteamNotStopped(StopOutcome.StillRunning);

        Assert.Equal(RemoveStatus.SteamStillRunning, summary.Status);
        Assert.Equal(MessageSeverity.Warning, summary.Severity);
        Assert.Equal(
            "Steam is still closing, so nothing was removed. Wait until it has closed, then try again. UWPHook will not start Steam again after this, so start it yourself.",
            summary.Message);
    }

    [Fact]
    public void ForUsers_WhenSteamCouldNotBeStartedAgain_SaysToStartIt_AsAWarning()
    {
        var summary = RemoveSummary.ForUsers(removed: 3, failed: 0, StopOutcome.NotStartedAgain);

        Assert.Equal(RemoveStatus.Removed, summary.Status);
        Assert.Equal(MessageSeverity.Warning, summary.Severity);
        Assert.Equal(
            "3 UWPHook shortcut(s) removed. Shortcuts from Steam and other tools were kept. Steam could not be started again; start it yourself.",
            summary.Message);
    }

    [Fact]
    public void ForUsers_WhenNothingWasRemoved_AndSteamCouldNotBeStartedAgain_SaysToStartIt()
    {
        var summary = RemoveSummary.ForUsers(removed: 0, failed: 1, StopOutcome.NotStartedAgain);

        Assert.Equal(RemoveStatus.Failed, summary.Status);
        Assert.EndsWith(" Steam could not be started again; start it yourself.", summary.Message);
    }

    [Fact]
    public void SteamNotStopped_WithoutSteamsPath_DoesNotClaimSteamWasAskedToClose()
    {
        var summary = RemoveSummary.SteamNotStopped(StopOutcome.ExecutableUnknown);

        Assert.Equal(RemoveStatus.SteamPathUnknown, summary.Status);
        Assert.Equal(MessageSeverity.Warning, summary.Severity);
        Assert.Equal(
            "Steam is running, but UWPHook could not find steam.exe to close and restart it, so nothing was removed. Close Steam, then try again.",
            summary.Message);
    }

    [Fact]
    public void ForUsers_WhenNothingWasRemoved_IsAnErrorNamingTheFailedCount()
    {
        var summary = RemoveSummary.ForUsers(removed: 0, failed: 2, StopOutcome.Stopped);

        Assert.Equal(RemoveStatus.Failed, summary.Status);
        Assert.Equal(MessageSeverity.Error, summary.Severity);
        Assert.Equal(
            "Removing UWPHook's shortcuts failed for 2 Steam account(s), so nothing was changed. Details are in application.log.",
            summary.Message);
    }

    [Fact]
    public void ForUsers_WithSteamRestarted_SaysSo()
    {
        var summary = RemoveSummary.ForUsers(removed: 3, failed: 0, StopOutcome.Stopped);

        Assert.Equal(RemoveStatus.Removed, summary.Status);
        Assert.Equal(MessageSeverity.Information, summary.Severity);
        Assert.Equal("3 UWPHook shortcut(s) removed. Shortcuts from Steam and other tools were kept. Steam has been restarted.", summary.Message);
    }

    [Fact]
    public void ForUsers_WithSteamNotRunning_SaysWhenTheChangesApply() =>
        Assert.Equal(
            "1 UWPHook shortcut(s) removed. Shortcuts from Steam and other tools were kept. The changes apply the next time you start Steam.",
            RemoveSummary.ForUsers(removed: 1, failed: 0, StopOutcome.NotRunning).Message);

    [Fact]
    public void ForUsers_WithSomeFailedAccounts_IsAWarningNamingThem()
    {
        var summary = RemoveSummary.ForUsers(removed: 1, failed: 1, StopOutcome.NotRunning);

        Assert.Equal(RemoveStatus.Removed, summary.Status);
        Assert.Equal(MessageSeverity.Warning, summary.Severity);
        Assert.Equal(
            "1 UWPHook shortcut(s) removed. Shortcuts from Steam and other tools were kept. Removing failed for 1 Steam account(s); details are in application.log. The changes apply the next time you start Steam.",
            summary.Message);
    }
}
