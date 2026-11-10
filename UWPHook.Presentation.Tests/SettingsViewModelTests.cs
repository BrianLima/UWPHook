using UWPHook.Core.Configuration;
using UWPHook.Core.Diagnostics;
using UWPHook.Core.Steam;
using UWPHook.Presentation.Settings;
using Xunit;

namespace UWPHook.Presentation.Tests;

public sealed class SettingsViewModelTests
{
    private const string Steam = @"C:\Steam";

    private static readonly SteamUser Alice = new("111", "Alice", "alice", MostRecent: true);
    private static readonly SteamUser Bob = SteamUser.Unnamed("222");

    private static readonly AppSettings Stored = AppSettings.Defaults with
    {
        Seconds = 3,
        StreamMode = true,
        LogLevel = AppLogLevel.Debug,
        SteamGridDbApiKey = "key",
        ArtworkStyle = "white_logo",
        ArtworkType = "animated",
        ArtworkNsfw = "any",
        ArtworkHumor = "true",
        Tags = "XBOX",
    };

    private readonly FakeSettings _settings = new(Stored);
    private readonly FakeDialogs _dialogs = new();
    private readonly FakeHost _host = new();
    private readonly RecordingLog _log = new();
    private readonly FakeRemover _remover = new(RemoveSummary.ForUsers(2, 0, StopOutcome.Stopped));
    private ISteamLocator _steam = new FakeSteam(Steam, Alice, Bob);
    private int _closes;

    private SettingsViewModel ViewModel()
    {
        var viewModel = new SettingsViewModel(_settings, _steam, _remover, _dialogs, _host, _log);
        viewModel.CloseRequested += (_, _) => _closes++;
        return viewModel;
    }

    // R38 / review minor 5: a binding source without INPC that holds its window leaks it (WPF PropertyDescriptor binding).
    [Fact]
    public void IsABindingSourceWithChangeNotification() =>
        Assert.IsAssignableFrom<System.ComponentModel.INotifyPropertyChanged>(ViewModel());

    // R38 / review minor 4: the question says what Remove really touches.
    [Fact]
    public void RemoveWarning_NamesTheSavedAccounts_AndOnlyUwpHooksShortcuts()
    {
        Assert.Contains("Steam accounts saved in Settings", SettingsViewModel.RemoveWarning);
        Assert.Contains("shortcuts UWPHook added", SettingsViewModel.RemoveWarning);
        Assert.DoesNotContain("every shortcut", SettingsViewModel.RemoveWarning);
    }

    [Fact]
    public void Title_HasTheVersion() => Assert.Equal("UWPHook version 2.15.0.0", ViewModel().Title);

    [Fact]
    public void Form_ShowsTheStoredSettings()
    {
        var form = ViewModel().Form;

        Assert.Equal(
            (2, true, (int)AppLogLevel.Debug, "key", 3, 2, 1, 2, "XBOX"),
            (form.SecondsIndex, form.StreamMode, form.LogLevelIndex, form.SteamGridDbApiKey, form.StyleIndex, form.TypeIndex, form.NsfwIndex, form.HumorIndex, form.Tags));
    }

    // "0 seconds" is gone: AppSettings clamps to 1..9.
    [Fact]
    public void SecondsChoices_AreOneToNine() =>
        Assert.Equal(Enumerable.Range(1, 9).Select(i => i + " seconds"), SettingsForm.SecondsChoices);

    [Fact]
    public void Save_StoresTheEditedSettings_AppliesTheLogLevel_AndCloses()
    {
        var viewModel = ViewModel();
        var form = viewModel.Form;
        form.SecondsIndex = 0;
        form.StreamMode = false;
        form.LogLevelIndex = (int)AppLogLevel.Trace;
        form.SteamGridDbApiKey = "new-key\r\n";
        form.StyleIndex = 5;
        form.TypeIndex = 1;
        form.NsfwIndex = 2;
        form.HumorIndex = 0;
        form.Tags = "A,B";

        viewModel.SaveCommand.Execute(null);

        Assert.Equal(
            Stored with
            {
                Seconds = 1,
                StreamMode = false,
                LogLevel = AppLogLevel.Trace,
                SteamGridDbApiKey = "new-key",
                ArtworkStyle = "no_logo",
                ArtworkType = "static",
                ArtworkNsfw = "true",
                ArtworkHumor = "false",
                Tags = "A,B",
            },
            _settings.Current);
        Assert.Equal(new[] { AppLogLevel.Trace }, _host.AppliedLevels);
        Assert.Equal(1, _closes);
    }

    [Fact]
    public void Save_WithNothingPicked_KeepsTheStoredValues()
    {
        var viewModel = ViewModel();
        var form = viewModel.Form;
        form.SecondsIndex = -1;
        form.LogLevelIndex = -1;
        form.StyleIndex = -1;
        form.TypeIndex = -1;
        form.NsfwIndex = -1;
        form.HumorIndex = -1;

        viewModel.SaveCommand.Execute(null);

        Assert.Equal(Stored, _settings.Current);
    }

    [Fact]
    public void Save_Failing_ReportsIt_AndKeepsTheWindowOpen()
    {
        var failure = new IOException("settings.json is locked");
        _settings.SaveFailure = failure;
        var viewModel = ViewModel();
        viewModel.Form.Tags = "lost";

        viewModel.SaveCommand.Execute(null);

        Assert.Equal((failure, "Saving your settings failed."), Assert.Single(_dialogs.Errors));
        Assert.Equal(0, _closes);
        Assert.Empty(_host.AppliedLevels);
        Assert.Equal("XBOX", _settings.Current.Tags);
    }

    // #76: the Steam user picker

    [Fact]
    public void Accounts_ListEverySteamAccount_AllTickedByDefault_WithTheMostRecentMarked()
    {
        var rows = ViewModel().Accounts.Rows;

        Assert.Equal(
            new[] { ("Alice (alice)", true, true), ("222", false, true) },
            rows.Select(row => (row.Label, row.IsMostRecent, row.IsChosen)));
    }

    [Theory]
    [InlineData("Alice", "alice", "Alice (alice)")]
    [InlineData("Alice", null, "Alice")]
    [InlineData(null, "alice", "alice")]
    [InlineData(null, null, "111")]
    [InlineData("", "", "111")]
    public void Label_IsTheNames_OrTheAccountId(string? persona, string? account, string expected) =>
        Assert.Equal(expected, SteamAccountRow.LabelFor(new SteamUser("111", persona, account, MostRecent: false)));

    [Fact]
    public void Accounts_TickOnlyTheStoredChoice()
    {
        _settings.Update(settings => settings with { ExportSteamAccounts = new SteamAccountChoice(["222"]) });

        Assert.Equal(new[] { false, true }, ViewModel().Accounts.Rows.Select(row => row.IsChosen));
    }

    [Fact]
    public void Accounts_WhenTheChosenAccountIsGone_AreAllTicked()
    {
        _settings.Update(settings => settings with { ExportSteamAccounts = new SteamAccountChoice(["999"]) });

        Assert.All(ViewModel().Accounts.Rows, row => Assert.True(row.IsChosen));
    }

    [Fact]
    public void Save_SomeAccountsTicked_StoresTheirIds()
    {
        var viewModel = ViewModel();
        viewModel.Accounts.Rows[0].IsChosen = false;

        viewModel.SaveCommand.Execute(null);

        Assert.Equal(new[] { "222" }, _settings.Current.ExportSteamAccounts.AccountIds);
    }

    [Fact]
    public void Save_EveryAccountTicked_StoresAll()
    {
        _settings.Update(settings => settings with { ExportSteamAccounts = new SteamAccountChoice(["222"]) });
        var viewModel = ViewModel();
        viewModel.Accounts.Rows[0].IsChosen = true;

        viewModel.SaveCommand.Execute(null);

        Assert.True(_settings.Current.ExportSteamAccounts.IsAll);
    }

    [Fact]
    public void Save_NoAccountTicked_AsksForOne_AndSavesNothing()
    {
        var viewModel = ViewModel();
        foreach (var row in viewModel.Accounts.Rows) row.IsChosen = false;

        viewModel.SaveCommand.Execute(null);

        Assert.Equal(new FakeDialogs.Shown(MessageSeverity.Warning, SettingsViewModel.ChooseAnAccount, "UWPHook"), Assert.Single(_dialogs.Messages));
        Assert.Equal(0, _settings.Saves);
        Assert.Equal(0, _closes);
    }

    [Fact]
    public void WithoutSteam_ListsNoAccounts_AndSavingKeepsTheStoredChoice()
    {
        _settings.Update(settings => settings with { ExportSteamAccounts = new SteamAccountChoice(["222"]) });
        _steam = new FakeSteam(null);
        var viewModel = ViewModel();

        viewModel.SaveCommand.Execute(null);

        Assert.False(viewModel.Accounts.HasAccounts);
        Assert.Equal(new SteamAccountChoice(["222"]), _settings.Current.ExportSteamAccounts);
        Assert.Equal(1, _closes);
    }

    [Fact]
    public void Accounts_WhenSteamCannotBeRead_AreNone_AndItIsLogged()
    {
        _steam = new ThrowingSteam();

        Assert.Empty(ViewModel().Accounts.Rows);
        Assert.Contains(_log.Entries, entry => entry.Level == LogLevel.Warning);
    }

    // Remove UWPHook shortcuts

    [Fact]
    public async Task Remove_AsksFirst_AndDoesNothingWhenDeclined()
    {
        _dialogs.Answer = false;

        await ViewModel().RemoveShortcutsCommand.ExecuteAsync(null);

        Assert.Equal((SettingsViewModel.RemoveWarning, ConfirmTone.Warning), Assert.Single(_dialogs.Questions));
        Assert.Empty(_remover.Calls);
    }

    // Manual-test finding 3: the remover asks (only when Steam runs and there is something to remove); the user answers.
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Remove_LetsTheUserDecide_WhetherSteamMayBeClosed(bool answer)
    {
        await ViewModel().RemoveShortcutsCommand.ExecuteAsync(null);
        _dialogs.Answer = answer;

        Assert.Equal(answer, Assert.Single(_remover.MayCloseSteam)());
        Assert.Equal(
            new[] { (SettingsViewModel.RemoveWarning, ConfirmTone.Warning), (CloseSteamQuestion.Text, ConfirmTone.Question) },
            _dialogs.Questions);
    }

    [Fact]
    public async Task Remove_WhenTheUserKeptSteamOpen_SaysNothingWasRemoved()
    {
        var remover = new FakeRemover(RemoveSummary.SteamNotStopped(StopOutcome.Declined));

        await new SettingsViewModel(_settings, _steam, remover, _dialogs, _host, _log).RemoveShortcutsCommand.ExecuteAsync(null);

        Assert.Equal(new FakeDialogs.Shown(MessageSeverity.Information, "Nothing was removed. Close Steam and try again.", "UWPHook"),
            Assert.Single(_dialogs.Messages));
    }

    [Fact]
    public async Task Remove_FromTheSavedAccounts_AndShowsWhatHappened()
    {
        _settings.Update(settings => settings with { ExportSteamAccounts = new SteamAccountChoice(["222"]) });

        await ViewModel().RemoveShortcutsCommand.ExecuteAsync(null);

        Assert.Equal(new SteamAccountChoice(["222"]), Assert.Single(_remover.Calls));
        var summary = RemoveSummary.ForUsers(2, 0, StopOutcome.Stopped);
        Assert.Equal(new FakeDialogs.Shown(summary.Severity, summary.Message, "UWPHook"), Assert.Single(_dialogs.Messages));
    }

    [Fact]
    public async Task Remove_ShowsAWarningSummary_AsAWarning()
    {
        var summary = RemoveSummary.SteamNotStopped(StopOutcome.StillRunning);
        var viewModel = new SettingsViewModel(_settings, _steam, new FakeRemover(summary), _dialogs, _host, _log);

        await viewModel.RemoveShortcutsCommand.ExecuteAsync(null);

        Assert.Equal(MessageSeverity.Warning, Assert.Single(_dialogs.Messages).Severity);
    }

    [Fact]
    public async Task Remove_Failing_ReportsIt()
    {
        var failure = new IOException("locked");
        _remover.Failure = failure;

        await ViewModel().RemoveShortcutsCommand.ExecuteAsync(null);

        Assert.Equal((failure, "Removing UWPHook's shortcuts failed."), Assert.Single(_dialogs.Errors));
    }

    // Closing the window (I1): Remove keeps running when Settings closes, and could then overlap an export.

    [Fact]
    public async Task WhileRemoving_RefusesToClose_AndSaysWhy()
    {
        _remover.Gate = new TaskCompletionSource();
        var viewModel = ViewModel();
        var removing = viewModel.RemoveShortcutsCommand.ExecuteAsync(null);

        Assert.False(viewModel.ConfirmClose());
        Assert.Equal(new FakeDialogs.Shown(MessageSeverity.Information, SettingsViewModel.StillRemoving, "UWPHook"), Assert.Single(_dialogs.Messages));

        _remover.Gate.SetResult();
        await removing;
        Assert.True(viewModel.ConfirmClose());
    }

    [Fact]
    public void WhenIdle_Closes_WithoutAsking()
    {
        Assert.True(ViewModel().ConfirmClose());
        Assert.Empty(_dialogs.Messages);
    }

    [Fact]
    public async Task AfterAFailedRemove_Closes()
    {
        _remover.Failure = new IOException("locked");
        var viewModel = ViewModel();

        await viewModel.RemoveShortcutsCommand.ExecuteAsync(null);

        Assert.True(viewModel.ConfirmClose());
    }

    // SteamGridDB key (manual-test finding 1): masked until the user asks to see it.

    [Fact]
    public void ApiKey_IsMaskedAtFirst()
    {
        var viewModel = ViewModel();

        Assert.False(viewModel.IsApiKeyShown);
        Assert.True(viewModel.IsApiKeyMasked);
    }

    [Fact]
    public void ShowingTheApiKey_UnmasksIt_AndTellsTheWindow()
    {
        var viewModel = ViewModel();
        var changed = new List<string?>();
        viewModel.PropertyChanged += (_, e) => changed.Add(e.PropertyName);

        viewModel.IsApiKeyShown = true;

        Assert.False(viewModel.IsApiKeyMasked);
        Assert.Equal(new[] { nameof(SettingsViewModel.IsApiKeyShown), nameof(SettingsViewModel.IsApiKeyMasked) }, changed);
    }

    // Links

    [Fact]
    public void GetApiKey_ExplainsWhatToDo_ThenOpensSteamGridDb()
    {
        ViewModel().GetApiKeyCommand.Execute(null);

        var shown = Assert.Single(_dialogs.Messages);
        Assert.Equal((MessageSeverity.Information, "Attention!"), (shown.Severity, shown.Title));
        Assert.StartsWith("You are being redirected to SteamGridDB website!", shown.Message);
        Assert.Equal(new[] { "https://www.steamgriddb.com/profile/preferences/api" }, _host.OpenedUrls);
    }

    [Fact]
    public void OpenLink_OpensTheUrl()
    {
        ViewModel().OpenLinkCommand.Execute("https://reddit.com/r/UWPHook/");

        Assert.Equal(new[] { "https://reddit.com/r/UWPHook/" }, _host.OpenedUrls);
    }

    private sealed class ThrowingSteam : ISteamLocator
    {
        public string? InstallFolder() => Steam;

        public string? SteamExecutable() => null;

        public IReadOnlyList<SteamUser> Users(string installFolder) => throw new UnauthorizedAccessException("userdata");
    }
}
