using CommunityToolkit.Mvvm.ComponentModel;
using UWPHook.Core.Steam;

namespace UWPHook.Presentation.Settings;

/// <summary>One Steam account in Settings' picker (#76), with a tick for whether UWPHook exports to it.</summary>
public sealed class SteamAccountRow(SteamUser user, bool isChosen) : ObservableObject
{
    private bool chosen = isChosen;

    public SteamUser User { get; } = user;

    public string Label { get; } = LabelFor(user);

    /// <summary>The account Steam signed in last.</summary>
    public bool IsMostRecent => User.MostRecent;

    public bool IsChosen
    {
        get => chosen;
        set => SetProperty(ref chosen, value);
    }

    /// <summary>"PersonaName (AccountName)", whichever of the two it has, or the account id when it has neither.</summary>
    public static string LabelFor(SteamUser user) => user.AccountId; // RED stub

    private static string? Named(string? name) => string.IsNullOrWhiteSpace(name) ? null : name;
}
