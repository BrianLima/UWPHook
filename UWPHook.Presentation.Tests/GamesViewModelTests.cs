using UWPHook.Core.Catalog;
using UWPHook.Core.Configuration;
using UWPHook.Core.Diagnostics;
using UWPHook.Core.Steam;
using UWPHook.Presentation.Games;
using Xunit;

namespace UWPHook.Presentation.Tests;

public sealed class GamesViewModelTests
{
    private const string UwpHookExe = @"C:\Users\me\AppData\Local\UWPHook\current\UWPHook.exe";

    private static readonly XboxApp Halo = App("Halo", "Microsoft.Halo_8wekyb3d8bbwe!App", "halo.exe");
    private static readonly XboxApp Forza = App("forza Horizon", "Microsoft.Forza_8wekyb3d8bbwe!Game", "forza.exe");
    private static readonly XboxApp Calculator = App("Calculator", "Microsoft.WindowsCalculator_8wekyb3d8bbwe!App", "calc.exe");

    private readonly FakeCatalog _catalog = new(Halo, Forza, Calculator);
    private readonly FakeIconPicker _icons = new();
    private readonly FakeExporter _exporter = new(ExportSummary.ForUsers(1, 0, StopOutcome.NotRunning));
    private readonly FakeSettings _settings = new(AppSettings.Defaults with { Tags = "XBOX, GAME PASS" });
    private readonly FakeDialogs _dialogs = new();
    private readonly RecordingLog _log = new();
    private int _exeLookups;

    private static XboxApp App(string name, string aumid, string exe) =>
        new(aumid, aumid.Split('!')[0], name, exe, @"C:\Apps\" + name, DateTimeOffset.UnixEpoch) { IconFolder = @"C:\Apps\" + name + @"\Assets" };

    private GamesViewModel ViewModel() => new(_catalog, _icons, _exporter, _settings, _dialogs, _log, () =>
    {
        _exeLookups++;
        return UwpHookExe;
    });

    private async Task<GamesViewModel> Loaded()
    {
        var games = ViewModel();
        await games.LoadCommand.ExecuteAsync(null);
        return games;
    }

    private static IEnumerable<string> Names(IEnumerable<AppRow> rows) => rows.Select(row => row.Name);

    // Loading

    [Fact]
    public void BeforeLoading_AsksToLoad()
    {
        var games = ViewModel();

        Assert.Equal(GamesViewModel.PressLoadStatus, games.Status);
        Assert.Empty(games.Apps.All);
        Assert.False(games.IsBusy);
    }

    [Fact]
    public async Task Load_ListsTheApps_ByName_IgnoringCase()
    {
        var games = await Loaded();

        Assert.Equal(new[] { "Calculator", "forza Horizon", "Halo" }, Names(games.Apps.Visible));
        Assert.Equal(GamesViewModel.LoadedStatus, games.Status);
        Assert.True(games.IsIdle);
    }

    [Fact]
    public async Task Load_MapsEachAppToARow()
    {
        var halo = Assert.Single((await Loaded()).Apps.All, row => row.Name == "Halo");

        Assert.Equal((Halo.Aumid, "halo.exe", Halo.IconFolder, false), (halo.Aumid, halo.Executable, halo.IconPath, halo.Selected));
    }

    // The 2.14 list included the system apps, so they stay on by default.
    [Fact]
    public async Task Load_IncludesTheSystemApps_ByDefault()
    {
        await Loaded();

        Assert.Equal(new[] { true }, _catalog.IncludeSystemAsked);
    }

    [Fact]
    public async Task Load_WithoutSystemApps_AsksTheCatalogWithoutThem()
    {
        var games = ViewModel();
        games.IncludeSystemApps = false;

        await games.LoadCommand.ExecuteAsync(null);

        Assert.Equal(new[] { false }, _catalog.IncludeSystemAsked);
    }

    [Fact]
    public async Task Load_Again_ReplacesTheList()
    {
        var games = await Loaded();
        _catalog.Apps = [Halo];

        await games.LoadCommand.ExecuteAsync(null);

        Assert.Equal(new[] { "Halo" }, Names(games.Apps.All));
    }

    [Fact]
    public async Task WhileLoading_IsBusy_AndNothingCanBeExported()
    {
        var games = ViewModel();
        _catalog.Gate = new TaskCompletionSource();

        var loading = games.LoadCommand.ExecuteAsync(null);

        Assert.True(games.IsBusy);
        Assert.False(games.IsIdle);
        Assert.Equal(GamesViewModel.LoadingStatus, games.Status);
        Assert.False(games.ExportCommand.CanExecute(null));
        _catalog.Gate.SetResult();
        await loading;
        Assert.False(games.IsBusy);
        Assert.True(games.ExportCommand.CanExecute(null));
    }

    [Fact]
    public async Task Load_Failing_ReportsIt_AndIsIdleAgain()
    {
        var failure = new InvalidOperationException("Appx is broken");
        _catalog.Failure = failure;

        var games = await Loaded();

        Assert.Equal((failure, "Loading your installed apps failed."), Assert.Single(_dialogs.Errors));
        Assert.True(games.IsIdle);
        Assert.Equal(GamesViewModel.LoadedStatus, games.Status);
        Assert.Empty(games.Apps.All);
    }

    // Icons: picked after the list is shown, off the UI thread, by the same rule as the export's.

    private const string HaloIcon = @"C:\Apps\Halo\Assets\Square150x150Logo.png";
    private const string ForzaIcon = @"C:\Apps\forza Horizon\Assets\Square310x310Logo.png";

    private static AppRow Row(GamesViewModel games, string name) => Assert.Single(games.Apps.All, row => row.Name == name);

    [Fact]
    public async Task Load_GivesEachRowTheIconPickedInItsLogoFolder()
    {
        _icons.Icons[Halo.IconFolder] = HaloIcon;
        _icons.Icons[Forza.IconFolder] = ForzaIcon;

        var games = await Loaded();
        await games.IconsLoaded;

        Assert.Equal(HaloIcon, Row(games, "Halo").Icon);
        Assert.Equal(ForzaIcon, Row(games, "forza Horizon").Icon);
        Assert.Null(Row(games, "Calculator").Icon);
        Assert.Equal(new[] { Calculator.IconFolder, Forza.IconFolder, Halo.IconFolder }.Order(StringComparer.Ordinal), _icons.Asked.Order(StringComparer.Ordinal));
    }

    [Fact]
    public async Task Load_ShowsTheList_BeforeTheIconsArrive()
    {
        _icons.Icons[Halo.IconFolder] = HaloIcon;
        _icons.Gate = new TaskCompletionSource();

        var games = await Loaded();

        Assert.Equal(new[] { "Calculator", "forza Horizon", "Halo" }, Names(games.Apps.Visible));
        Assert.True(games.IsIdle);
        Assert.Null(Row(games, "Halo").Icon);

        var changed = new List<string?>();
        Row(games, "Halo").PropertyChanged += (_, e) => changed.Add(e.PropertyName);
        _icons.Gate.SetResult();
        await games.IconsLoaded;

        Assert.Equal(HaloIcon, Row(games, "Halo").Icon);
        Assert.Equal(new[] { nameof(AppRow.Icon) }, changed);
    }

    [Fact]
    public async Task Load_Again_CancelsTheIconsOfTheFirstLoad()
    {
        _icons.Icons[Halo.IconFolder] = HaloIcon;
        _icons.Gate = new TaskCompletionSource();
        var games = await Loaded();
        var firstRows = games.Apps.All.ToList();
        var firstPass = games.IconsLoaded;

        await games.LoadCommand.ExecuteAsync(null);
        _icons.Gate.SetResult();
        await firstPass;
        await games.IconsLoaded;

        Assert.All(firstRows, row => Assert.Null(row.Icon));
        Assert.Equal(HaloIcon, Row(games, "Halo").Icon);
    }

    [Fact]
    public async Task Load_WhenPickingAnIconThrows_LeavesThatRowWithout_AndLogsItAtDebug()
    {
        _icons.Icons[Forza.IconFolder] = ForzaIcon;
        _icons.Failing.Add(Halo.IconFolder);

        var games = await Loaded();
        await games.IconsLoaded;

        Assert.Null(Row(games, "Halo").Icon);
        Assert.Equal(ForzaIcon, Row(games, "forza Horizon").Icon);
        var logged = Assert.Single(_log.Entries, entry => entry.Exception is IOException);
        Assert.Equal(LogLevel.Debug, logged.Level);
        Assert.Empty(_dialogs.Errors);
    }

    // Review minor 7: with hundreds of apps, the icons of the rows on screen come first.
    [Fact]
    public async Task VisibleFirst_ListsTheFilteredRowsFirst_ThenTheRest_EachInListOrder()
    {
        var games = await Loaded();

        games.Apps.Filter = "h";

        Assert.Equal(new[] { "forza Horizon", "Halo", "Calculator" }, Names(games.Apps.VisibleFirst()));
    }

    [Fact]
    public async Task Export_SendsTheIconTheListShows()
    {
        _icons.Icons[Halo.IconFolder] = HaloIcon;
        var games = await LoadedWithSelected("Halo", "Calculator");
        await games.IconsLoaded;

        await games.ExportCommand.ExecuteAsync(null);

        Assert.Equal(new[] { null, HaloIcon }, Assert.Single(_exporter.Requests).Apps.Select(app => app.Icon));
    }

    // The export does not wait for the icon pass: a row whose icon is not in yet is sent without one, and the
    // export picks it itself.
    [Fact]
    public async Task Export_BeforeTheIconsArrive_SendsNoIcon()
    {
        _icons.Icons[Halo.IconFolder] = HaloIcon;
        _icons.Gate = new TaskCompletionSource();
        var games = await LoadedWithSelected("Halo");

        await games.ExportCommand.ExecuteAsync(null);

        Assert.Null(Assert.Single(Assert.Single(_exporter.Requests).Apps).Icon);
        _icons.Gate.SetResult();
        await games.IconsLoaded;
        Assert.Equal(HaloIcon, Row(games, "Halo").Icon);
    }

    // Search and selection

    [Theory]
    [InlineData("HALO", new[] { "Halo" })]
    [InlineData("windowscalc", new[] { "Calculator" })]
    [InlineData("h", new[] { "forza Horizon", "Halo" })]
    [InlineData("", new[] { "Calculator", "forza Horizon", "Halo" })]
    public async Task Filter_MatchesTheNameOrTheAumid_IgnoringCase(string filter, string[] expected)
    {
        var games = await Loaded();

        games.Apps.Filter = filter;

        Assert.Equal(expected, Names(games.Apps.Visible));
    }

    // R38 / review I1: the grid is bound to Visible, so it must stay the same collection (2.14 kept the sort,
    // the columns and an edit in progress while filtering).
    [Fact]
    public async Task Filter_KeepsTheSameVisibleCollection_AndUpdatesItInPlace()
    {
        var games = await Loaded();
        var visible = games.Apps.Visible;
        var halo = Assert.Single(visible, row => row.Name == "Halo");

        games.Apps.Filter = "h";
        var narrowed = Names(games.Apps.Visible).ToList();
        games.Apps.Filter = "";

        Assert.Same(visible, games.Apps.Visible);
        Assert.Equal(new[] { "forza Horizon", "Halo" }, narrowed);
        Assert.Equal(new[] { "Calculator", "forza Horizon", "Halo" }, Names(visible));
        Assert.Same(halo, Assert.Single(visible, row => row.Name == "Halo"));
    }

    [Fact]
    public async Task Filter_ChangingIt_NeverReplacesVisible()
    {
        var games = await Loaded();
        var changed = new List<string?>();
        games.Apps.PropertyChanged += (_, e) => changed.Add(e.PropertyName);

        games.Apps.Filter = "halo";
        await games.LoadCommand.ExecuteAsync(null);

        Assert.DoesNotContain(nameof(AppList.Visible), changed);
        Assert.Equal(new[] { "Halo" }, Names(games.Apps.Visible));
    }

    [Fact]
    public async Task SelectAll_SelectsTheVisibleApps()
    {
        var games = await Loaded();
        games.Apps.Filter = "h";

        games.SelectAllCommand.Execute(null);

        Assert.Equal(new[] { "forza Horizon", "Halo" }, Names(games.Apps.All.Where(row => row.Selected)));
    }

    [Fact]
    public async Task SelectNone_ClearsTheVisibleApps()
    {
        var games = await Loaded();
        foreach (var row in games.Apps.All) row.Selected = true;
        games.Apps.Filter = "halo";

        games.SelectNoneCommand.Execute(null);

        Assert.Equal(new[] { "Calculator", "forza Horizon" }, Names(games.Apps.All.Where(row => row.Selected)));
    }

    // Export

    private async Task<GamesViewModel> LoadedWithSelected(params string[] names)
    {
        var games = await Loaded();
        foreach (var row in games.Apps.All.Where(row => names.Contains(row.Name))) row.Selected = true;
        return games;
    }

    [Fact]
    public async Task Export_SendsTheSelectedApps_WithTheTagsAndAccountsFromSettings()
    {
        _settings.Update(settings => settings with { ExportSteamAccounts = new SteamAccountChoice(["222"]) });
        var games = await LoadedWithSelected("Halo", "Calculator");
        Assert.Single(games.Apps.All, row => row.Name == "Halo").Name = "Halo MCC";

        await games.ExportCommand.ExecuteAsync(null);

        var request = Assert.Single(_exporter.Requests);
        Assert.Equal(
            new[]
            {
                new ExportApp("Calculator", Calculator.Aumid, "calc.exe", Calculator.IconFolder),
                new ExportApp("Halo MCC", Halo.Aumid, "halo.exe", Halo.IconFolder),
            },
            request.Apps);
        Assert.Equal(new[] { "XBOX", "GAME PASS" }, request.Tags);
        Assert.Equal(new SteamAccountChoice(["222"]), request.Accounts);
        Assert.Equal(UwpHookExe, request.FindUwpHookExe());
    }

    // 10c-1 minor 1: finding UWPHook.exe is left to the exporter, after its pre-checks.
    [Fact]
    public async Task Export_DoesNotLookForUwpHookExe_Itself()
    {
        var games = await LoadedWithSelected("Halo");

        await games.ExportCommand.ExecuteAsync(null);

        Assert.Equal(0, _exeLookups);
    }

    // Manual-test finding 3: the exporter asks (only when Steam runs, after its pre-checks); the user answers.
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Export_LetsTheUserDecide_WhetherSteamMayBeClosed(bool answer)
    {
        var games = await LoadedWithSelected("Halo");
        _dialogs.Answer = answer;

        await games.ExportCommand.ExecuteAsync(null);
        Assert.Empty(_dialogs.Questions); // asking is the exporter's call

        Assert.Equal(answer, Assert.Single(_exporter.Requests).MayCloseSteam());
        Assert.Equal((CloseSteamQuestion.Text, ConfirmTone.Question), Assert.Single(_dialogs.Questions));
    }

    [Fact]
    public void CloseSteamQuestion_SaysWhyAndThatSteamComesBack()
    {
        Assert.Contains("Steam must be closed to write your shortcuts.", CloseSteamQuestion.Text);
        Assert.Contains("Close Steam now?", CloseSteamQuestion.Text);
        Assert.Contains("started again afterwards", CloseSteamQuestion.Text);
    }

    public static TheoryData<ExportSummary, MessageSeverity> Summaries => new()
    {
        { ExportSummary.ForUsers(2, 0, StopOutcome.Stopped), MessageSeverity.Information },
        { ExportSummary.NothingSelected(), MessageSeverity.Information },
        { ExportSummary.ForUsers(1, 1, StopOutcome.NotRunning), MessageSeverity.Warning },
        { ExportSummary.SteamStillRunning(), MessageSeverity.Warning },
        { ExportSummary.SteamNotStopped(StopOutcome.Declined), MessageSeverity.Information },
        { ExportSummary.ForUsers(1, 0, StopOutcome.NotRunning).WithArtworkFailures(["111"]), MessageSeverity.Warning },
        { ExportSummary.SteamNotFound(), MessageSeverity.Error },
        { ExportSummary.ForUsers(0, 2, StopOutcome.NotRunning), MessageSeverity.Error },
    };

    // Fix-wave minor 8: what the window used to map, now tested.
    [Theory]
    [MemberData(nameof(Summaries))]
    public async Task Export_ShowsTheSummary_WithItsSeverity(ExportSummary summary, MessageSeverity expected)
    {
        var exporter = new FakeExporter(summary);
        var games = new GamesViewModel(_catalog, _icons, exporter, _settings, _dialogs, _log, () => UwpHookExe);
        await games.LoadCommand.ExecuteAsync(null);

        await games.ExportCommand.ExecuteAsync(null);

        Assert.Equal(new FakeDialogs.Shown(expected, summary.Message, "UWPHook"), Assert.Single(_dialogs.Messages));
        Assert.Contains(_log.Entries, entry => entry.Level == LogLevel.Information && entry.Template.StartsWith("Export finished"));
    }

    [Fact]
    public async Task Export_Failing_ReportsIt_AndIsIdleAgain()
    {
        var failure = new IOException("disk full");
        _exporter.Failure = failure;
        var games = await LoadedWithSelected("Halo");

        await games.ExportCommand.ExecuteAsync(null);

        Assert.Equal((failure, "Exporting your games failed."), Assert.Single(_dialogs.Errors));
        Assert.Empty(_dialogs.Messages);
        Assert.True(games.IsIdle);
        Assert.True(games.ExportCommand.CanExecute(null));
    }

    [Fact]
    public async Task Export_RaisesIsBusy_WhileItRuns()
    {
        var games = await LoadedWithSelected("Halo");
        var busy = new List<bool>();
        games.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(GamesViewModel.IsBusy)) busy.Add(games.IsBusy);
        };

        await games.ExportCommand.ExecuteAsync(null);

        Assert.Equal(new[] { true, false }, busy);
    }

    // Closing the window (I1): an export killed by closing the app would leave Steam closed.

    [Fact]
    public async Task WhileExporting_RefusesToClose_AndSaysWhy()
    {
        var games = ViewModel();
        _exporter.Gate = new TaskCompletionSource();
        var exporting = games.ExportCommand.ExecuteAsync(null);

        Assert.False(games.ConfirmClose());
        Assert.Equal(new FakeDialogs.Shown(MessageSeverity.Information, GamesViewModel.StillExporting, "UWPHook"), Assert.Single(_dialogs.Messages));

        _exporter.Gate.SetResult();
        await exporting;
        Assert.True(games.ConfirmClose());
    }

    [Fact]
    public void WhenIdle_Closes_WithoutAsking()
    {
        Assert.True(ViewModel().ConfirmClose());
        Assert.Empty(_dialogs.Messages);
    }

    // Loading touches neither Steam nor its files, so it doesn't hold the window open.
    [Fact]
    public async Task WhileLoading_Closes()
    {
        var games = ViewModel();
        _catalog.Gate = new TaskCompletionSource();
        var loading = games.LoadCommand.ExecuteAsync(null);

        Assert.True(games.ConfirmClose());
        Assert.Empty(_dialogs.Messages);

        _catalog.Gate.SetResult();
        await loading;
    }

    [Fact]
    public async Task AfterAFailedExport_Closes()
    {
        _exporter.Failure = new IOException("locked");
        var games = ViewModel();

        await games.ExportCommand.ExecuteAsync(null);

        Assert.True(games.ConfirmClose());
    }

    // The first-run SteamGridDB offer

    [Fact]
    public void OfferSteamGridDb_TheFirstTime_RemembersIt_AndAsks()
    {
        _dialogs.Answer = true;

        var openSettings = ViewModel().OfferSteamGridDbOnce();

        Assert.True(openSettings);
        Assert.True(_settings.Current.OfferedSteamGridDb);
        Assert.Equal((GamesViewModel.SteamGridDbOffer, ConfirmTone.Question), Assert.Single(_dialogs.Questions));
    }

    [Fact]
    public void OfferSteamGridDb_Declined_DoesNotOpenSettings()
    {
        _dialogs.Answer = false;

        Assert.False(ViewModel().OfferSteamGridDbOnce());
        Assert.True(_settings.Current.OfferedSteamGridDb);
    }

    [Fact]
    public void OfferSteamGridDb_AfterItWasOffered_DoesNotAsk()
    {
        _settings.Update(settings => settings with { OfferedSteamGridDb = true });

        Assert.False(ViewModel().OfferSteamGridDbOnce());
        Assert.Empty(_dialogs.Questions);
    }

    [Fact]
    public void OfferSteamGridDb_WhenSavingFails_LogsIt_AndStillAsks()
    {
        _settings.SaveFailure = new IOException("settings.json is locked");

        Assert.True(ViewModel().OfferSteamGridDbOnce());
        Assert.Single(_dialogs.Questions);
        Assert.Contains(_log.Entries, entry => entry.Level == LogLevel.Error && entry.Exception == _settings.SaveFailure);
    }
}
