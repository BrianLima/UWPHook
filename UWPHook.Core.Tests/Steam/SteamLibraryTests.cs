using UWPHook.Core.Diagnostics;
using UWPHook.Core.Steam;
using UWPHook.Core.Tests.Diagnostics;
using Xunit;

namespace UWPHook.Core.Tests.Steam;

public sealed class SteamLibraryTests : IDisposable
{
    private const string MovedSteam = @"D:\Steam";
    private const string StaleSteam = @"C:\Program Files (x86)\Steam";
    private const string OtherStaleSteam = @"C:\Program Files\Steam";

    private readonly string _steam = Path.Combine(Path.GetTempPath(), "uwphook-steam-" + Guid.NewGuid().ToString("N"));
    private readonly RecordingLog _log = new();

    public void Dispose()
    {
        if (Directory.Exists(_steam)) Directory.Delete(_steam, recursive: true);
    }

    private static Func<string, bool> Existing(params string[] paths) =>
        path => paths.Contains(path, StringComparer.OrdinalIgnoreCase);

    private SteamLibrary Library(ISteamRegistry registry, Func<string, bool>? folderExists = null, Func<string, bool>? fileExists = null) =>
        new(registry, _log, folderExists, fileExists);

    private SteamLibrary Library() => Library(new FakeSteamRegistry(null));

    private void AccountFolders(params string[] names)
    {
        foreach (var name in names) Directory.CreateDirectory(Path.Combine(_steam, "userdata", name));
    }

    private void LoginUsers(string content)
    {
        Directory.CreateDirectory(Path.Combine(_steam, "config"));
        File.WriteAllText(Path.Combine(_steam, "config", "loginusers.vdf"), content);
    }

    private void LoginUsersFixture()
    {
        Directory.CreateDirectory(Path.Combine(_steam, "config"));
        File.Copy(Path.Combine(AppContext.BaseDirectory, "Steam", "Fixtures", "loginusers.vdf"), Path.Combine(_steam, "config", "loginusers.vdf"));
    }

    // Install folder (I5): HKCU SteamPath first, then each HKLM view.

    [Fact]
    public void InstallFolder_PrefersTheCurrentUsersSteamPath() =>
        Assert.Equal(MovedSteam, Library(new FakeSteamRegistry("D:/Steam", StaleSteam), Existing(MovedSteam, StaleSteam)).InstallFolder());

    [Fact]
    public void InstallFolder_FallsBackToTheInstallPath_WhenTheSteamPathFolderIsGone() =>
        Assert.Equal(StaleSteam, Library(new FakeSteamRegistry(MovedSteam, StaleSteam), Existing(StaleSteam)).InstallFolder());

    // Fix-wave minor 4: a stale 32-bit view must not hide a valid 64-bit one.
    [Fact]
    public void InstallFolder_TriesEveryRegistryView_UntilOneFolderExists() =>
        Assert.Equal(OtherStaleSteam, Library(new FakeSteamRegistry(null, StaleSteam, OtherStaleSteam), Existing(OtherStaleSteam)).InstallFolder());

    [Fact]
    public void InstallFolder_SkipsAViewWithoutAnInstallPath() =>
        Assert.Equal(OtherStaleSteam, Library(new FakeSteamRegistry(null, null, OtherStaleSteam), Existing(OtherStaleSteam)).InstallFolder());

    [Fact]
    public void InstallFolder_IsNull_WhenNoFolderExists() =>
        Assert.Null(Library(new FakeSteamRegistry(MovedSteam, StaleSteam, OtherStaleSteam), Existing()).InstallFolder());

    [Fact]
    public void SteamExecutable_IsSteamExeInTheInstallFolder()
    {
        var exe = Path.Combine(MovedSteam, "steam.exe");

        Assert.Equal(exe, Library(new FakeSteamRegistry(MovedSteam), Existing(MovedSteam), Existing(exe)).SteamExecutable());
    }

    [Fact]
    public void SteamExecutable_IsNull_WhenSteamExeIsMissing() =>
        Assert.Null(Library(new FakeSteamRegistry(MovedSteam), Existing(MovedSteam), Existing()).SteamExecutable());

    [Fact]
    public void SteamExecutable_IsNull_WhenSteamWasNotFound() =>
        Assert.Null(Library(new FakeSteamRegistry(null), Existing(), _ => true).SteamExecutable());

    // Users (M6): numeric, non-zero userdata folders, named from loginusers.vdf.

    [Fact]
    public void Users_ListsOnlyNumericNonZeroAccountFolders()
    {
        AccountFolders("67890", "12345", "0", "anonymous", "ac");

        var users = Library().Users(_steam);

        Assert.Equal(new[] { SteamUser.Unnamed("12345"), SteamUser.Unnamed("67890") }, users);
    }

    [Fact]
    public void Users_WithoutAUserdataFolder_IsEmpty()
    {
        Directory.CreateDirectory(_steam);

        Assert.Empty(Library().Users(_steam));
    }

    [Fact]
    public void Users_AreNamedFromLoginUsers_ByTheirSteamId()
    {
        AccountFolders("22202", "39734272");
        LoginUsersFixture();

        var users = Library().Users(_steam);

        Assert.Equal(
            new[]
            {
                new SteamUser("22202", "Rabscuttle", "gaben", MostRecent: true),
                new SteamUser("39734272", "Zwölf Ünïcode", "second_account", MostRecent: false),
            },
            users);
    }

    [Fact]
    public void Users_AFolderWithoutALoginUsersEntry_IsStillAUser_WithoutAName()
    {
        AccountFolders("22202", "55555");
        LoginUsersFixture();

        var users = Library().Users(_steam);

        Assert.Equal(SteamUser.Unnamed("55555"), Assert.Single(users, user => user.AccountId == "55555"));
    }

    [Fact]
    public void Users_ALoginUsersEntryWithoutAFolder_IsNotAUser()
    {
        AccountFolders("22202");
        LoginUsersFixture();

        Assert.Equal("22202", Assert.Single(Library().Users(_steam)).AccountId);
    }

    [Fact]
    public void Users_WithoutALoginUsersFile_AreTheFolders_AndNothingIsLogged()
    {
        AccountFolders("22202");

        Assert.Equal(SteamUser.Unnamed("22202"), Assert.Single(Library().Users(_steam)));
        Assert.Empty(_log.Entries);
    }

    [Theory]
    [InlineData("")]
    [InlineData("not a vdf {{{")]
    [InlineData("\"users\" \"not an object\"")]
    [InlineData("\"users\"\n{\n\t\"76561197960287930\"\n\t{\n\t\t\"PersonaName\"")]
    public void Users_WithACorruptLoginUsersFile_AreTheFolders_WithoutThrowing(string content)
    {
        AccountFolders("22202", "12345");
        LoginUsers(content);

        var users = Library().Users(_steam);

        Assert.Equal(new[] { SteamUser.Unnamed("12345"), SteamUser.Unnamed("22202") }, users);
    }

    [Fact]
    public void Users_WithATruncatedLoginUsersFile_LogAWarning()
    {
        AccountFolders("22202");
        LoginUsers("\"users\"\n{\n\t\"76561197960287930\"\n\t{\n\t\t\"PersonaName\"");

        Library().Users(_steam);

        Assert.Single(_log.At(LogLevel.Warning));
    }

    [Fact]
    public void Users_ALoginUsersEntryWithoutNames_GivesNullNames()
    {
        AccountFolders("22202");
        LoginUsers("\"users\"\n{\n\t\"76561197960287930\"\n\t{\n\t\t\"MostRecent\"\t\t\"1\"\n\t}\n}\n");

        Assert.Equal(new SteamUser("22202", null, null, MostRecent: true), Assert.Single(Library().Users(_steam)));
    }

    [Fact]
    public void FolderIn_IsTheAccountsUserdataFolder() =>
        Assert.Equal(Path.Combine(_steam, "userdata", "22202"), SteamUser.Unnamed("22202").FolderIn(_steam));
}
