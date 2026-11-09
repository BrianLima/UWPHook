using CommunityToolkit.Mvvm.Input;
using UWPHook.Core.Configuration;
using UWPHook.Core.Diagnostics;
using UWPHook.Core.Steam;

namespace UWPHook.Presentation.Settings;

/// <summary>
/// The Settings window: the stored settings to edit (<see cref="Form"/>), the Steam accounts to export to
/// (<see cref="Accounts"/>), Save, "Remove UWPHook shortcuts" and the links.
/// </summary>
public sealed class SettingsViewModel
{
    public const string ChooseAnAccount = "Choose at least one Steam account to export your games to.";

    public const string RemoveWarning =
        "This action will remove every shortcut UWPHook added to Steam. Non-Steam games added by Steam or other tools are kept, and shortcuts.vdf is backed up first."
        + "\r\nAre you sure you want to continue?";

    private const string ApiKeyPage = "https://www.steamgriddb.com/profile/preferences/api";

    private const string ApiKeyHelp = "You are being redirected to SteamGridDB website!\r\n" +
        "Log-in, or create your account, go to your profile preferences and click 'Generate API Key', then paste the key back on UWPHook.";

    private readonly ISettingsService settings;
    private readonly IShortcutRemover remover;
    private readonly IDialogService dialogs;
    private readonly IAppHost host;
    private readonly ILog log;

    public SettingsViewModel(ISettingsService settings, ISteamLocator steam, IShortcutRemover remover, IDialogService dialogs, IAppHost host, ILog log)
    {
        this.settings = settings;
        this.remover = remover;
        this.dialogs = dialogs;
        this.host = host;
        this.log = log;
        Form = SettingsForm.From(settings.Current);
        Accounts = SteamAccountList.Load(steam, settings.Current.ExportSteamAccounts, log);
        SaveCommand = new RelayCommand(Save);
        RemoveShortcutsCommand = new AsyncRelayCommand(RemoveShortcutsAsync);
        OpenLinkCommand = new RelayCommand<string>(_ => { }); // RED stub
        GetApiKeyCommand = new RelayCommand(GetApiKey);
    }

    /// <summary>Raised after a successful save: the window closes.</summary>
    public event EventHandler? CloseRequested;

    public string Title => "UWPHook version " + host.Version;

    public SettingsForm Form { get; }

    public SteamAccountList Accounts { get; }

    public IRelayCommand SaveCommand { get; }
    public IAsyncRelayCommand RemoveShortcutsCommand { get; }
    public IRelayCommand<string> OpenLinkCommand { get; }
    public IRelayCommand GetApiKeyCommand { get; }

    /// <summary>Saves and closes; when saving fails the error is shown and the window stays open with the edits.</summary>
    private void Save()
    {
        // RED stub
    }

    /// <summary>From the saved accounts, off the UI thread; the command is disabled while it runs.</summary>
    private async Task RemoveShortcutsAsync()
    {
        await Task.CompletedTask; // RED stub
    }

    private void RequestClose() => CloseRequested?.Invoke(this, EventArgs.Empty); // RED stub: not called yet

    private void GetApiKey()
    {
        _ = (settings, remover, dialogs, host, log); // RED stub
    }
}
