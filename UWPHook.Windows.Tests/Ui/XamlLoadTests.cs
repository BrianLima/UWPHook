using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using UWPHook.Core.Catalog;
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
    public void Splash_IsDark()
    {
        var (mode, background) = UiThread.Run(() =>
        {
            var splash = new FullScreenLauncher();
            LayOut(splash);
            return (splash.ThemeMode, (splash.Background as SolidColorBrush)?.Color);
        });

        Assert.Equal(ThemeMode.Dark, mode);
        Assert.Equal(Color.FromRgb(0x20, 0x20, 0x20), background); // Fluent's dark ApplicationBackgroundColor
    }

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

    // The stream splash is closed before the game is activated, and never stays above other windows.
    [Fact]
    public void Splash_IsNotTopmost() =>
        Assert.False(UiThread.Run(() => new FullScreenLauncher().Topmost));

    // Manual-test finding 1, Settings at its minimum size. An element is clipped when it is arranged larger than the
    // slot its parent gave it (WPF then clips it to the slot); its DesiredSize is no help, as WPF caps it to the slot.
    [Fact]
    public void Settings_AtItsMinimumSize_ClipsNothing()
    {
        var (minWidth, clipped) = UiThread.Run(() =>
        {
            var (window, _) = NewSettings();
            LayOut(window, 700, 750);
            return (window.MinWidth, Clipped((FrameworkElement)window.Content).ToList());
        });

        if (clipped.Count > 0) Assert.Fail("Clipped: " + string.Join("; ", clipped));
        Assert.True(minWidth >= 700, $"MinWidth is {minWidth}");
    }

    [Fact]
    public void Settings_FieldLabels_DoNotOverlapTheInputs()
    {
        var overlaps = UiThread.Run(() =>
        {
            var (window, _) = NewSettings();
            LayOut(window, 700, 750);
            var root = (FrameworkElement)window.Content;
            var labels = Descendants(root).OfType<TextBlock>().Where(label => label.Name.StartsWith("label_", StringComparison.Ordinal)).ToList();
            var inputs = Descendants(root).OfType<FrameworkElement>().Where(element => element is TextBox or PasswordBox or ComboBox or ItemsControl or ButtonBase).ToList();
            Assert.True(labels.Count >= 8, $"found {labels.Count} field labels");
            return (from label in labels
                    from input in inputs
                    where Overlap(BoundsIn(root, label), BoundsIn(root, input)) && !IsWithin(label, input)
                    select $"{label.Name} overlaps {Describe(input)}").ToList();
        });

        Assert.Empty(overlaps);
    }

    [Fact]
    public void Settings_ListsTheSteamAccounts_OneAboveTheOther_AllInView()
    {
        var (first, second, extent, viewport) = UiThread.Run(() =>
        {
            var (window, _) = NewSettings();
            LayOut(window, 700, 750);
            var list = Assert.IsType<ItemsControl>(window.FindName("accounts_list"));
            var scroller = Assert.IsType<ScrollViewer>(window.FindName("accounts_scroller"));
            var rows = Enumerable.Range(0, 2)
                .Select(i => Assert.IsAssignableFrom<FrameworkElement>(list.ItemContainerGenerator.ContainerFromIndex(i)))
                .Select(row => row.TransformToAncestor(list).Transform(new Point()))
                .ToList();
            return (rows[0], rows[1], scroller.ExtentHeight, scroller.ViewportHeight);
        });

        Assert.Equal(first.X, second.X);
        Assert.True(second.Y > first.Y, $"the second account is at {second}, the first at {first}");
        Assert.True(extent <= viewport, $"two accounts need scrolling ({extent} > {viewport})");
    }

    [Fact]
    public void Settings_ApiKey_IsMaskedUntilShown()
    {
        var (maskedAtFirst, clearAtFirst, password, maskedWhenShown, clearWhenShown) = UiThread.Run(() =>
        {
            var (window, settings) = NewSettings(AppSettings.Defaults with { SteamGridDbApiKey = "secret-key" });
            LayOut(window);
            var masked = Assert.IsType<PasswordBox>(window.FindName("apiKey_passwordBox"));
            var clear = Assert.IsType<TextBox>(window.FindName("steamgriddb_api_key"));
            var (maskedAtFirst, clearAtFirst, password) = (masked.Visibility, clear.Visibility, masked.Password);

            settings.IsApiKeyShown = true;
            LayOut(window);
            return (maskedAtFirst, clearAtFirst, password, masked.Visibility, clear.Visibility);
        });

        Assert.Equal((Visibility.Visible, Visibility.Collapsed), (maskedAtFirst, clearAtFirst));
        Assert.Equal("secret-key", password);
        Assert.Equal((Visibility.Collapsed, Visibility.Visible), (maskedWhenShown, clearWhenShown));
    }

    [Fact]
    public void Settings_ApiKey_MaskedBoxAndForm_StayInStep()
    {
        var (typed, edited) = UiThread.Run(() =>
        {
            var (window, settings) = NewSettings();
            LayOut(window);
            var masked = Assert.IsType<PasswordBox>(window.FindName("apiKey_passwordBox"));

            masked.Password = "typed-key";
            var typed = settings.Form.SteamGridDbApiKey;
            settings.Form.SteamGridDbApiKey = "edited-key";
            return (typed, masked.Password);
        });

        Assert.Equal("typed-key", typed);
        Assert.Equal("edited-key", edited);
    }

    // Manual-test finding 2: Fluent's DataGrid paints itself white when disabled, so it must stay enabled while busy;
    // an overlay in the window's own (theme) background covers it instead.
    [Fact]
    public void GamesWindow_WhileBusy_KeepsTheListEnabled_UnderAnOverlayInTheWindowsBackground()
    {
        var (busy, listEnabled, overlayVisibility, overlayColor, windowColor) = UiThread.Run(() =>
        {
            var gate = new TaskCompletionSource();
            var window = NewGamesWindow(new FakeCatalog { Gate = gate }); // loads at once, and waits for the gate
            LayOut(window);
            var list = Assert.IsType<DataGrid>(window.FindName("listGames"));
            var overlay = Assert.IsType<Border>(window.FindName("busyOverlay"));
            var result = (((GamesViewModel)window.DataContext).IsBusy, list.IsEnabled, overlay.Visibility,
                (overlay.Background as SolidColorBrush)?.Color, (window.Background as SolidColorBrush)?.Color);
            gate.SetResult();
            return result;
        });

        Assert.True(busy);
        Assert.True(listEnabled, "a disabled Fluent DataGrid turns white");
        Assert.Equal(Visibility.Visible, overlayVisibility);
        Assert.Equal(Assert.NotNull(windowColor), Assert.NotNull(overlayColor));
    }

    // The icon column: the same columns as before, in the same order, with the icon as a thumbnail instead of two
    // text columns (the picked file and the logo folder).
    [Fact]
    public void GamesWindow_Columns_AreSelectedIconNameExecutableAumid_WithOnlyTheNameEditable()
    {
        var (headers, iconColumn, readOnly) = UiThread.Run(() =>
        {
            var window = NewGamesWindow();
            LayOut(window);
            var columns = Assert.IsType<DataGrid>(window.FindName("listGames")).Columns;
            return (columns.Select(column => column.Header as string).ToList(),
                columns.Single(column => column.Header as string == "Icon").GetType(),
                columns.Where(column => column.ReadLocalValue(DataGridColumn.IsReadOnlyProperty) is true).Select(column => column.Header as string).ToList());
        });

        Assert.Equal(new[] { "Selected", "Icon", "Name", "Executable", "Aumid" }, headers);
        Assert.Equal(typeof(DataGridTemplateColumn), iconColumn);
        Assert.Equal(new[] { "Icon", "Executable", "Aumid" }, readOnly);
    }

    [Fact]
    public void GamesWindow_IconCell_ShowsTheThumbnail_WithTheLogoFolderAsItsTooltip()
    {
        var folder = Path.Combine(Path.GetTempPath(), "UWPHook.Windows.Tests", Guid.NewGuid().ToString("N"));
        var png = TestImages.Write(Path.Combine(folder, "Square150x150Logo.png"), 150, 150, System.Drawing.Imaging.ImageFormat.Png);
        try
        {
            var (width, tooltip) = UiThread.Run(() =>
            {
                var gate = new TaskCompletionSource();
                var window = NewGamesWindow(new FakeCatalog { Gate = gate }); // the load waits, so only the row below is listed
                var games = (GamesViewModel)window.DataContext;
                var row = new AppRow(new XboxApp("Microsoft.Halo_8wekyb3d8bbwe!App", "Microsoft.Halo_8wekyb3d8bbwe", "Halo", "halo.exe", folder,
                    DateTimeOffset.UnixEpoch)) { Icon = png };
                games.Apps.Replace([row]);
                LayOut(window);
                var list = Assert.IsType<DataGrid>(window.FindName("listGames"));
                var image = Assert.Single(Descendants(list).OfType<Image>(), image => image.Name == "iconThumbnail");
                var result = ((image.Source as BitmapSource)?.PixelWidth, Tooltip(image, list));
                gate.SetResult();
                return result;
            });

            Assert.Equal(IconThumbnailConverter.Size, width);
            Assert.Equal(folder, tooltip);
        }
        finally
        {
            Directory.Delete(folder, recursive: true);
        }
    }

    /// <summary>The nearest tooltip on <paramref name="element"/> or its ancestors, up to <paramref name="root"/>.</summary>
    private static object? Tooltip(FrameworkElement element, FrameworkElement root) =>
        AncestorsUpTo(root, element).Select(ancestor => ancestor.ToolTip).FirstOrDefault(tip => tip is not null);

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

    private static GamesWindow NewGamesWindow(FakeCatalog? catalog = null)
    {
        // Already offered, so nothing opens Settings as a dialog.
        var settings = new FakeSettings(AppSettings.Defaults with { OfferedSteamGridDb = true });
        var games = new GamesViewModel(catalog ?? new FakeCatalog(), new FakeIconPicker(), new FakeExporter(ExportSummary.NothingSelected()), settings,
            new FakeDialogs(), new RecordingLog(), () => @"C:\Users\me\AppData\Local\UWPHook\UWPHook.exe");
        return new GamesWindow(games, NewSettingsWindow);
    }

    private static SettingsWindow NewSettingsWindow() => NewSettings().Window;

    private static (SettingsWindow Window, SettingsViewModel Settings) NewSettings(AppSettings? stored = null)
    {
        var steam = new FakeSteam(@"C:\Steam", new SteamUser("111", "Alice", "alice", MostRecent: true), SteamUser.Unnamed("222"));
        var settings = new SettingsViewModel(new FakeSettings(stored ?? AppSettings.Defaults), steam,
            new FakeRemover(RemoveSummary.ForUsers(1, 0, StopOutcome.Stopped)), new FakeDialogs(), new FakeHost(), new RecordingLog());
        return (new SettingsWindow(settings), settings);
    }

    /// <summary>Applies the templates and styles without showing the window (no desktop needed).</summary>
    private static void LayOut(Window window)
    {
        // Let any binding update WPF queued (at DataBind priority) run first.
        window.Dispatcher.Invoke(DispatcherPriority.DataBind, new Action(() => { }));
        window.ApplyTemplate();
        window.Measure(new Size(window.Width, window.Height));
        window.Arrange(new Rect(0, 0, window.Width, window.Height));
        window.UpdateLayout();
    }

    /// <summary>
    /// Lays the window's content out at <paramref name="width"/> x <paramref name="height"/>. A window that was never
    /// shown has no HWND, and WPF's Window then measures but never arranges its content, so the content is arranged
    /// here, as the window would (its frame left out).
    /// </summary>
    private static void LayOut(Window window, double width, double height)
    {
        window.Width = width;
        window.Height = height;
        LayOut(window);

        var content = (FrameworkElement)window.Content;
        content.Measure(new Size(width, height));
        content.Arrange(new Rect(0, 0, width, height));
        content.UpdateLayout();
    }

    /// <summary>
    /// The visible elements under <paramref name="root"/> that are arranged larger than their layout slot (less their
    /// margin), which WPF clips to the slot. Content inside a ScrollViewer gets a slot as large as itself, so only what
    /// can't be scrolled to counts. Checked: the window's own elements and the content controls show (a combo box's
    /// selected text), with every element up to <paramref name="root"/> they sit in, so a control too narrow for its
    /// content counts too. The theme's own template parts are checked only as such containers. Images that fill
    /// their box (UniformToFill) are cropped on purpose.
    /// </summary>
    private static IEnumerable<string> Clipped(FrameworkElement root) =>
        Descendants(root).OfType<FrameworkElement>()
            .Where(element => element.TemplatedParent is null or ContentPresenter)
            .SelectMany(element => AncestorsUpTo(root, element))
            .Distinct()
            .Where(element => element is not Image { Stretch: Stretch.UniformToFill } && IsClipped(element))
            .Select(element =>
            {
                var slot = LayoutInformation.GetLayoutSlot(element);
                return $"{Describe(element)}: {element.ActualWidth:0.#}x{element.ActualHeight:0.#} in a {slot.Width:0.#}x{slot.Height:0.#} slot";
            });

    private static bool IsClipped(FrameworkElement element)
    {
        var slot = LayoutInformation.GetLayoutSlot(element);
        var width = slot.Width - element.Margin.Left - element.Margin.Right;
        var height = slot.Height - element.Margin.Top - element.Margin.Bottom;
        return element.ActualWidth > width + 0.5 || element.ActualHeight > height + 0.5;
    }

    /// <summary><paramref name="element"/> and its visual ancestors, up to (not including) <paramref name="root"/>.</summary>
    private static IEnumerable<FrameworkElement> AncestorsUpTo(FrameworkElement root, FrameworkElement element)
    {
        for (DependencyObject? current = element; current is not null && current != root; current = VisualTreeHelper.GetParent(current))
        {
            if (current is FrameworkElement framework) yield return framework;
        }
    }

    /// <summary>Every visible element under <paramref name="root"/> (not inside a collapsed or hidden one).</summary>
    private static IEnumerable<UIElement> Descendants(DependencyObject root)
    {
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
        {
            if (VisualTreeHelper.GetChild(root, i) is not UIElement child || child.Visibility != Visibility.Visible) continue;

            yield return child;
            foreach (var descendant in Descendants(child)) yield return descendant;
        }
    }

    private static Rect BoundsIn(Visual root, FrameworkElement element) =>
        element.TransformToAncestor(root).TransformBounds(new Rect(0, 0, element.ActualWidth, element.ActualHeight));

    /// <summary>The rectangles share an area (not just an edge).</summary>
    private static bool Overlap(Rect a, Rect b)
    {
        var shared = Rect.Intersect(a, b);
        return !shared.IsEmpty && shared.Width > 0 && shared.Height > 0;
    }

    private static bool IsWithin(DependencyObject element, DependencyObject ancestor)
    {
        for (var current = element; current is not null; current = VisualTreeHelper.GetParent(current))
        {
            if (current == ancestor) return true;
        }

        return false;
    }

    private static string Describe(FrameworkElement element)
    {
        var text = element switch
        {
            TextBlock block => $" \"{block.Text}\"",
            ContentControl { Content: string content } => $" \"{content}\"",
            _ => "",
        };
        var name = string.IsNullOrEmpty(element.Name) ? "" : " " + element.Name;
        var owner = element.TemplatedParent is FrameworkElement parent ? $" (in {parent.GetType().Name} {parent.Name})" : "";
        return element.GetType().Name + name + text + owner;
    }
}
