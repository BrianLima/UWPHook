using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using UWPHook.Core.Configuration;
using UWPHook.Core.Steam;
using UWPHook.Presentation.Games;
using UWPHook.Presentation.Settings;
using UWPHook.Presentation.Tests;
using Xunit;

// ThemeMode is WPF's experimental Fluent API (WPF0001); these tests read it on purpose.
#pragma warning disable WPF0001

namespace UWPHook.Windows.Tests.Ui;

/// <summary>
/// Loads App.xaml and builds every window on fake ViewModels, then lays it out, so a style or template that
/// names a missing resource (a <c>StaticResource</c> throws while loading) fails here instead of on a user's PC.
/// </summary>
public sealed class XamlLoadTests
{
    public const string IconFont = "Segoe Fluent Icons, Segoe MDL2 Assets";

    private static readonly string[] WindowNames = ["Games", "Settings", "Splash"];

    public static TheoryData<string> Windows => new(WindowNames);

    // The keys the windows look up with DynamicResource: a missing one does not throw, it just leaves the default.
    public static readonly TheoryData<string> ThemeKeys =
    [
        "ApplicationBackgroundBrush", // Fluent
        "TextFillColorPrimaryBrush", // Fluent
        "AccentButtonStyle", // Fluent
        "IconGlyph", // App.xaml
        "HeaderBand", // App.xaml
        "SectionHeader", // App.xaml
    ];

    [Fact]
    public void App_FollowsTheSystemTheme() =>
        Assert.Equal(ThemeMode.System, UiThread.Run(() => Application.Current.ThemeMode));

    [Theory]
    [MemberData(nameof(ThemeKeys))]
    public void App_HasTheResourcesTheWindowsUse(string key) =>
        Assert.NotNull(UiThread.Run(() => Application.Current.TryFindResource(key)));

    [Theory]
    [MemberData(nameof(Windows))]
    public void Window_LoadsAndLaysOut_OnAnOpaqueBackground(string name)
    {
        var background = UiThread.Run(() =>
        {
            var window = NewWindow(name);
            LayOut(window);
            return (window.Background as SolidColorBrush)?.Color;
        });

        Assert.Equal((byte)255, Assert.NotNull(background).A);
    }

    // The stream splash goes full screen over the game: dark whatever the system theme is.
    [Fact]
    public void Splash_IsDark() =>
        Assert.Equal(ThemeMode.Dark, UiThread.Run(() => new FullScreenLauncher().ThemeMode));

    [Theory]
    [InlineData("settingsButton", "\uE713")]
    [InlineData("reloadButton", "\uE72C")]
    public void GamesWindow_ButtonIcons_AreFluentGlyphs_WithTheWindows10Fallback(string button, string glyph)
    {
        var (text, font) = UiThread.Run(() =>
        {
            var window = NewGamesWindow();
            LayOut(window);
            var icon = Assert.IsType<TextBlock>(Assert.IsType<Button>(window.FindName(button)).Content);
            return (icon.Text, icon.FontFamily.Source);
        });

        Assert.Equal(glyph, text);
        Assert.Equal(IconFont, font);
    }

    [Fact]
    public void NoWindow_LoadsMaterialDesign()
    {
        UiThread.Run(() =>
        {
            foreach (var name in WindowNames) LayOut(NewWindow(name));
        });

        Assert.DoesNotContain(AppDomain.CurrentDomain.GetAssemblies(),
            assembly => assembly.GetName().Name!.StartsWith("MaterialDesign", StringComparison.Ordinal));
    }

    private static Window NewWindow(string name) => name switch
    {
        "Games" => NewGamesWindow(),
        "Settings" => NewSettingsWindow(),
        "Splash" => new FullScreenLauncher(),
        _ => throw new ArgumentOutOfRangeException(nameof(name), name, null),
    };

    private static GamesWindow NewGamesWindow()
    {
        // Already offered, so nothing opens Settings as a dialog.
        var settings = new FakeSettings(AppSettings.Defaults with { OfferedSteamGridDb = true });
        var games = new GamesViewModel(new FakeCatalog(), new FakeExporter(ExportSummary.NothingSelected()), settings,
            new FakeDialogs(), new RecordingLog(), () => @"C:\Users\me\AppData\Local\UWPHook\UWPHook.exe");
        return new GamesWindow(games, NewSettingsWindow);
    }

    private static SettingsWindow NewSettingsWindow()
    {
        var steam = new FakeSteam(@"C:\Steam", new SteamUser("111", "Alice", "alice", MostRecent: true), SteamUser.Unnamed("222"));
        return new SettingsWindow(new SettingsViewModel(new FakeSettings(AppSettings.Defaults), steam,
            new FakeRemover(RemoveSummary.ForUsers(1, 0, StopOutcome.Stopped)), new FakeDialogs(), new FakeHost(), new RecordingLog()));
    }

    /// <summary>Applies the templates and styles without showing the window (no desktop needed).</summary>
    private static void LayOut(Window window)
    {
        window.ApplyTemplate();
        window.Measure(new Size(window.Width, window.Height));
        window.Arrange(new Rect(0, 0, window.Width, window.Height));
        window.UpdateLayout();
    }
}
