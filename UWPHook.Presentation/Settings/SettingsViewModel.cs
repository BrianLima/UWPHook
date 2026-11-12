using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using UWPHook.Core.Configuration;
using UWPHook.Core.Diagnostics;
using UWPHook.Core.Steam;

namespace UWPHook.Presentation.Settings;

/// <summary>
/// The Settings window: the stored settings to edit (<see cref="Form"/>), the Steam accounts to export to
/// (<see cref="Accounts"/>), Save, "Remove UWPHook shortcuts" and the links.
/// </summary>
/// <remarks>An <see cref="ObservableObject"/>: WPF leaks a window bound to a source without change notification.</remarks>
public sealed class SettingsViewModel : ObservableObject
{
    public const string ChooseAnAccount = "Choose at least one Steam account to export your games to.";

    public const string RemoveWarning =
        "This removes the shortcuts UWPHook added to Steam, from the Steam accounts saved in Settings (ticks you have not saved yet don't count). "
        + "Non-Steam games added by Steam or other tools are kept, and shortcuts.vdf is backed up first."
        + "\r\nAre you sure you want to continue?";

    public const string StillRemoving = "UWPHook is still removing its shortcuts. Please wait until it finishes: it may have closed Steam, and it starts Steam again when it is done.";

    private const string ApiKeyPage = "https://www.steamgriddb.com/profile/preferences/api";

    private const string ApiKeyHelp = "You are being redirected to SteamGridDB website!\r\n" +
        "Log-in, or create your account, go to your profile preferences and click 'Generate API Key', then paste the key back on UWPHook.";

    private readonly ISettingsService settings;
    private readonly IShortcutRemover remover;
    private readonly IDialogService dialogs;
    private readonly IAppHost host;
    private readonly ILog log;
    private bool isRemoving;
    private bool isApiKeyShown;

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
        OpenLinkCommand = new RelayCommand<string>(url => host.OpenUrl(url!));
        GetApiKeyCommand = new RelayCommand(GetApiKey);
    }

    /// <summary>Raised after a successful save: the window closes.</summary>
    public event EventHandler? CloseRequested;

    public string Title => "UWPHook version " + host.Version;

    public SettingsForm Form { get; }

    /// <summary>The seconds combo box's items; see <see cref="SettingsForm.SecondsChoices"/>.</summary>
    public IReadOnlyList<string> SecondsChoices => SettingsForm.SecondsChoices;

    public SteamAccountList Accounts { get; }

    /// <summary>Whether the SteamGridDB key is shown in clear; it is masked until the user asks to see it.</summary>
    public bool IsApiKeyShown
    {
        get => isApiKeyShown;
        set
        {
            if (SetProperty(ref isApiKeyShown, value)) OnPropertyChanged(nameof(IsApiKeyMasked));
        }
    }

    /// <summary>The opposite of <see cref="IsApiKeyShown"/>, for the masked box's visibility.</summary>
    public bool IsApiKeyMasked => !IsApiKeyShown;

    public IRelayCommand SaveCommand { get; }
    public IAsyncRelayCommand RemoveShortcutsCommand { get; }
    public IRelayCommand<string> OpenLinkCommand { get; }
    public IRelayCommand GetApiKeyCommand { get; }

    /// <summary>Saves and closes; when saving fails the error is shown and the window stays open with the edits.</summary>
    private void Save()
    {
        if (Accounts.NoneChosen)
        {
            dialogs.Show(MessageSeverity.Warning, ChooseAnAccount);
            return;
        }

        try
        {
            settings.Update(current => Accounts.ApplyTo(Form.ApplyTo(current)));
        }
        catch (Exception e)
        {
            dialogs.ReportError(e, "Saving your settings failed.");
            return;
        }

        host.ApplyLogLevel(settings.Current.LogLevel);
        CloseRequested?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>From the saved accounts, off the UI thread; the command is disabled while it runs.</summary>
    private async Task RemoveShortcutsAsync()
    {
        if (!dialogs.Confirm(RemoveWarning, ConfirmTone.Warning)) return;

        isRemoving = true;
        try
        {
            var accounts = settings.Current.ExportSteamAccounts;
            var summary = await Task.Run(() => remover.RemoveAsync(accounts, CloseSteamQuestion.AskWith(dialogs)));
            log.Information("Remove finished: {Summary}", summary);
            dialogs.Show(summary.Severity, summary.Message);
        }
        catch (Exception e)
        {
            dialogs.ReportError(e, "Removing UWPHook's shortcuts failed.");
        }
        finally
        {
            isRemoving = false;
        }
    }

    /// <summary>
    /// Whether the window may close now; when it may not, tells the user why. Not while removing: the removal
    /// would go on with Steam possibly closed, behind a closed window.
    /// </summary>
    public bool ConfirmClose()
    {
        if (!isRemoving) return true;

        dialogs.Show(MessageSeverity.Information, StillRemoving);
        return false;
    }

    private void GetApiKey()
    {
        dialogs.Show(MessageSeverity.Information, ApiKeyHelp, "Attention!");
        host.OpenUrl(ApiKeyPage);
    }
}
