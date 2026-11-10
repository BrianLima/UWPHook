using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using UWPHook.Core.Catalog;
using UWPHook.Core.Configuration;
using UWPHook.Core.Diagnostics;
using UWPHook.Core.Steam;

namespace UWPHook.Presentation.Games;

/// <summary>
/// The games window: load the installed apps, search and select them, and export the selected ones to Steam,
/// showing what really happened (<see cref="ExportSummary"/>). The list is locked while it loads or exports.
/// </summary>
/// <param name="uwpHookExe">The stable UWPHook.exe path; the exporter asks for it after its pre-checks.</param>
public sealed class GamesViewModel : ObservableObject
{
    public const string PressLoadStatus = "Press 'Load installed apps' to get a list of installed titles.";
    public const string LoadingStatus = "Loading your installed apps";
    public const string LoadedStatus = "Installed Apps";
    public const string SteamGridDbOffer = "Do you want to automatically import grid images for imported games?";
    public const string StillExporting = "UWPHook is still exporting your games. Please wait until it finishes: it may have closed Steam, and it starts Steam again when it is done.";

    private readonly IAppCatalog catalog;
    private readonly IShortcutExporter exporter;
    private readonly ISettingsService settings;
    private readonly IDialogService dialogs;
    private readonly ILog log;
    private readonly Func<string> uwpHookExe;
    private bool isBusy;
    private bool isExporting;
    private string status = PressLoadStatus;
    private bool includeSystemApps = true;

    public GamesViewModel(IAppCatalog catalog, IShortcutExporter exporter, ISettingsService settings, IDialogService dialogs, ILog log, Func<string> uwpHookExe)
    {
        this.catalog = catalog;
        this.exporter = exporter;
        this.settings = settings;
        this.dialogs = dialogs;
        this.log = log;
        this.uwpHookExe = uwpHookExe;
        LoadCommand = new AsyncRelayCommand(LoadAsync, () => !IsBusy);
        ExportCommand = new AsyncRelayCommand(ExportAsync, () => !IsBusy);
        SelectAllCommand = new RelayCommand(() => Apps.SelectVisible(true));
        SelectNoneCommand = new RelayCommand(() => Apps.SelectVisible(false));
    }

    public AppList Apps { get; } = new();

    public IAsyncRelayCommand LoadCommand { get; }
    public IAsyncRelayCommand ExportCommand { get; }
    public IRelayCommand SelectAllCommand { get; }
    public IRelayCommand SelectNoneCommand { get; }

    /// <summary>The 2.14 list included the system apps, so they are on by default.</summary>
    public bool IncludeSystemApps
    {
        get => includeSystemApps;
        set => SetProperty(ref includeSystemApps, value);
    }

    public string Status
    {
        get => status;
        private set => SetProperty(ref status, value);
    }

    public bool IsBusy
    {
        get => isBusy;
        private set
        {
            if (!SetProperty(ref isBusy, value)) return;
            OnPropertyChanged(nameof(IsIdle));
            LoadCommand.NotifyCanExecuteChanged();
            ExportCommand.NotifyCanExecuteChanged();
        }
    }

    public bool IsIdle => !IsBusy;

    private async Task LoadAsync()
    {
        IsBusy = true;
        Status = LoadingStatus;
        Apps.Replace([]);
        try
        {
            var includeSystem = IncludeSystemApps;
            var apps = await Task.Run(() => catalog.GetInstalledAppsAsync(includeSystem, CancellationToken.None));
            Apps.Replace(apps.OrderBy(app => app.DisplayName, StringComparer.CurrentCultureIgnoreCase).Select(app => new AppRow(app)));
        }
        catch (Exception e)
        {
            dialogs.ReportError(e, "Loading your installed apps failed.");
        }
        finally
        {
            IsBusy = false;
            Status = LoadedStatus;
        }
    }

    private async Task ExportAsync()
    {
        var current = settings.Current;
        var request = new ExportRequest([.. Apps.Selected.Select(row => row.ToExportApp())], current.Tags, uwpHookExe)
        {
            Accounts = current.ExportSteamAccounts,
            MayCloseSteam = CloseSteamQuestion.AskWith(dialogs),
        };

        if (await ExportedAsync(request) is { } summary)
            dialogs.Show(summary.Severity, summary.Message);
    }

    /// <summary>Off the UI thread (icons, downloads, file writes). Null when the export threw.</summary>
    private async Task<ExportSummary?> ExportedAsync(ExportRequest request)
    {
        IsBusy = true;
        isExporting = true;
        try
        {
            var summary = await Task.Run(() => exporter.ExportAsync(request));
            log.Information("Export finished: {Summary}", summary);
            return summary;
        }
        catch (Exception e)
        {
            dialogs.ReportError(e, "Exporting your games failed.");
            return null;
        }
        finally
        {
            isExporting = false;
            IsBusy = false;
        }
    }

    /// <summary>
    /// Whether the window may close now; when it may not, tells the user why. Not while exporting (I1): closing the
    /// app would end the export while Steam may be closed, and Steam would not be started again. Loading may be cut short.
    /// </summary>
    public bool ConfirmClose()
    {
        if (!isExporting) return true;

        dialogs.Show(MessageSeverity.Information, StillExporting);
        return false;
    }

    /// <summary>
    /// The first time only: offers to set up SteamGridDB. True when the user wants Settings opened. A key the user
    /// already entered is kept; a failure to remember the offer is only logged.
    /// </summary>
    public bool OfferSteamGridDbOnce()
    {
        if (settings.Current.OfferedSteamGridDb) return false;

        try
        {
            settings.Update(current => current with { OfferedSteamGridDb = true });
        }
        catch (Exception e)
        {
            log.Error(e, "Saving that SteamGridDB was offered failed");
        }

        return dialogs.Confirm(SteamGridDbOffer, ConfirmTone.Question);
    }
}
