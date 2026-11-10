# UWPHook Modernization Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Turn UWPHook 2.14.3 (broken .NET 8 port, unreproducible build) into a stable 2.15 release now, then a clean, tested 3.0 core on .NET 10.

**Architecture:** Add a cross-platform `UWPHook.Core` library (`net10.0`, no WPF and no Windows APIs) that holds the pure logic: shortcut ids, launch-argument parsing, shortcut matching/building, the Steam-restart policy and artwork naming. It is unit-tested on any OS. The WPF app `UWPHook` (`net10.0-windows10.0.19041.0`) becomes a thin shell over Core plus Windows adapters (COM activation, PackageManager, processes, registry). Phase 1 keeps the current behaviour and fixes regressions. Phase 2 swaps PowerShell for PackageManager and VDFParser for ValveKeyValue behind Core interfaces.

**Tech Stack:** C# 14 / .NET 10 SDK, WPF with the built-in Fluent theme (MaterialDesign removed in Task 12), Velopack, xUnit v3, ValveKeyValue (Phase 2), System.IO.Hashing, System.Net.Http.Json, Serilog, GitHub Actions (windows-latest + ubuntu-latest).

**Spec:** `UWPHook-Status-Report.md` (the companion report; section numbers below refer to it).

## Decisions (2026-11-08, from Brian)

1. **UI theme:** move to WPF's built-in **Fluent theme** (`Application.ThemeMode`, .NET 9+) and **remove MaterialDesignThemes/MaterialDesignColors** (Task 12).
2. **Installer/updates:** **Velopack** (Task 7 + Phase 3 item 8).
3. **Install location:** **per-user** (Velopack default under `%LocalAppData%`, no admin).
4. **Code signing:** decide later.
5. **Process:** **TDD** for all new and changed behaviour. Every task starts with a failing test. Code without a test is not done. Building up the test suite is a first-class goal.
6. **Execution:** subagent-driven, on branch `modernize/2.15-stabilize`.

## Execution prerequisites (blocking)

These tasks need a working `dotnet` SDK plus NuGet restore. The 2026-11-08 cloud session had neither (`dot.net`, `builds.dotnet.microsoft.com` and `api.nuget.org` were blocked by egress policy), and `git push` was refused because the Claude GitHub App isn't installed on the repo. Run the plan where at least one of these is true:
- **(a)** a local Claude Code session on Brian's Windows PC (best: also covers the manual WPF checks), or
- **(b)** a cloud session with network access for the .NET/NuGet hosts allowed, or
- **(c)** the Claude GitHub App installed on `BrianLima/UWPHook`, so the branch can be pushed and GitHub Actions (Task 2 CI) becomes the test runner (slow red/green loop).

## Global Constraints

- SDK pinned by `global.json`: `"version": "10.0.100", "rollForward": "latestFeature"`.
- TFMs: `UWPHook.Core` and tests → `net10.0`. `UWPHook` → `net10.0-windows10.0.19041.0` with `<SupportedOSPlatformVersion>10.0.17763.0</SupportedOSPlatformVersion>`.
- `<Nullable>enable</Nullable>`, `<TreatWarningsAsErrors>true</TreatWarningsAsErrors>` for Core and tests. The app may start with warnings-as-errors off. **No blanket `<NoWarn>`** (rejecting PR #163's approach).
- Central Package Management (`Directory.Packages.props`). No `HintPath` references to DLLs outside the repo.
- Version lives only in `Directory.Build.props` (`<Version>2.15.0</Version>` for Phase 1).
- Keep the existing on-disk locations: `%AppData%\Briano\UWPHook\{application.log,backups,icons}`.
- Shortcut launch contract stays: `Exe = "<quoted UWPHook.exe path>"`, `StartDir = "<quoted dir>"`, `LaunchOptions = "<AUMID> <executable> [extra]"`.
- The shortcut `Exe` must be a path that survives updates. Under Velopack that is `%LocalAppData%\UWPHook\current\UWPHook.exe` — **not** the root launcher stub, which starts the app and exits (breaking Steam's playtime/streaming tracking); `current\` keeps its path across updates (ruling R24, 2026-11-08).
- **Never delete or rewrite shortcuts UWPHook did not create.** Always back up `shortcuts.vdf` before writing.
- Code style per Sandi Metz (POODR / 99 Bottles): small classes with one responsibility, dependencies injected, no static mutable state in Core, methods ≤ ~10 lines where practical, and tests target public behaviour.
- Executors in a Linux cloud session: install the SDK with `dotnet-install.sh --channel 10.0`. Build and test Core there. The WPF app builds only with `EnableWindowsTargeting=true` and must be smoke-tested on Windows by the user (manual checks are listed per task).

## Review Focus

1. **Existing users' shortcuts written by 2.14.3 (unquoted `Exe`) and by ≤2.14.2 (quoted).** Re-export must update in place (same appid, so artwork and collections stay) and must not duplicate. Test: Task 4 `Merge_UpdatesUnquotedLegacyEntry_KeepingAppId`.
2. **Steam already running with several `steam` processes, or refusing to exit.** Export must finish within the timeout and report "restart Steam manually", never hang. Test: `WhileStopped_GivesUpAfterTimeout_AndDoesNotWrite` (was Task 5 `Restart_GivesUpAfterTimeout`).
3. **Launch args split by an unquoted path with spaces** (all 2.14.3 shortcuts in `Program Files`). Launch must still find the AUMID. Test: Task 3 `Parse_RecoversAumidFromSplitUnquotedExePath`.
4. **Non-ASCII app names** (e.g. `Taiko no Tatsujin: The Drum Master!`, Japanese titles). Ids and vdf round-trip must use UTF-8. Test: Task 2 `Compute_UsesUtf8Bytes` and Task 9 `RoundTrip_PreservesUtf8Names`.
5. **Shortcuts from other tools with keys UWPHook doesn't model** (`sortas`, `FlatpakAppID`, `DevkitOverrideAppID`). They must survive a write. Test: Task 9 `RoundTrip_PreservesUnknownKeys`.

---

## Phase 0/1 — Foundation + 2.15 "Stabilize" release

### File structure after Phase 1

```
global.json                         (new)
Directory.Build.props               (new) shared props + version
Directory.Packages.props            (new) central package versions
UWPHook.slnx                        (new, replaces UWPHook.sln)
.github/workflows/ci.yml            (new)
VDFParser/                          (new, vendored from BrianLima/VDFParser@master, SDK-style, net10.0; deleted in Task 9)
UWPHook.Core/                       (new)
  Steam/SteamShortcutId.cs          crc-based appid
  Steam/ShortcutSpec.cs             record: what UWPHook wants in shortcuts.vdf
  Steam/ShortcutPaths.cs            quoting/unquoting helpers
  Steam/ShortcutMerger.cs           add/update UWPHook entries, preserve others
  Steam/SteamRestarter.cs           bounded restart policy
  Steam/ISteamProcess.cs
  Launch/LaunchRequest.cs           record + Parse(args)
  Artwork/ArtworkFile.cs            grid file naming by kind
UWPHook.Core.Tests/                 (new) xUnit v3, mirrors Core folders
UWPHook/                            (existing WPF app, modified)
```

### Task 1: Reproducible .NET 10 solution

**Files:**
- Create: `global.json`, `Directory.Build.props`, `Directory.Packages.props`, `UWPHook.slnx`, `VDFParser/VDFParser.csproj` (+ copied `*.cs`, `Machines/`, `Models/` from BrianLima/VDFParser, keep its MIT header)
- Modify: `UWPHook/UWPHook.csproj`, `README.md` ("Building" section)
- Delete: `UWPHook.sln`, `UWPHook/App.config`, `UWPHook/Properties/AssemblyInfo.cs` (move metadata to props: Company `Briano`, Product `UWPHook`, Description `Add your UWP games and apps to Steam!`)

**Interfaces:**
- Produces: buildable `UWPHook.slnx` with projects `UWPHook`, `VDFParser`. Namespace `VDFParser` unchanged, so the app code compiles untouched.

- [ ] **Step 1:** `UWPHook.csproj`:
  - Set TFM `net10.0-windows10.0.19041.0`.
  - Remove both `VDFParser` `HintPath` references and use `<ProjectReference Include="..\VDFParser\VDFParser.csproj" />`.
  - Remove the packages `Microsoft.AspNet.WebApi.Client`, `System.Memory`, `System.Security.Principal.Windows` and `System.Management.Automation`. Keep `Microsoft.PowerShell.SDK` until Task 6.
  - Remove `IncludeAllContentForSelfExtract`. Add `EnableCompressionInSingleFile=true`.
  - Add `<RuntimeIdentifiers>win-x64;win-arm64</RuntimeIdentifiers>` in place of the single RID.
  - In the vendored `VDFParser/Machines/VDFFieldsSM.cs`, replace `results.Add(tmpFieldName, …)` with `results[tmpFieldName] = …` (last value wins). This fixes "key already exists" (#139) until Task 9 replaces the parser.
- [ ] **Step 2:** Replace `ReadAsAsync<T>` in `SteamGridDbApi` with `ReadFromJsonAsync<T>` (System.Net.Http.Json, using `JsonSerializerOptions(JsonSerializerDefaults.Web)`). Replace `Assembly.GetExecutingAssembly().Location` uses (log path in `GamesWindow`, `ClearAllShortcuts`) with `Environment.ProcessPath` and `AppContext.BaseDirectory`. Log folder: `Path.Combine(AppData, "Briano", "UWPHook", "application.log")` (hard-coded, not from FileVersionInfo).
- [ ] **Step 3:** Verify:
  - Run `dotnet build UWPHook.slnx -c Release -p:EnableWindowsTargeting=true`. Expected: `Build succeeded`.
  - Run `dotnet publish UWPHook/UWPHook.csproj -c Release -r win-x64`. Expected: a single `UWPHook.exe` with no temp extraction.
  - *Manual (Windows):* the app opens and Settings shows the version `2.15.0.0`.
- [ ] **Step 4:** Commit `build: retarget to .NET 10, vendor VDFParser, central package management`.

### Task 2: Core library, test project, CI, `SteamShortcutId`

**Files:**
- Create: `UWPHook.Core/UWPHook.Core.csproj` (`net10.0`, package `System.IO.Hashing`), `UWPHook.Core/Steam/SteamShortcutId.cs`, `UWPHook.Core.Tests/UWPHook.Core.Tests.csproj`, `UWPHook.Core.Tests/Steam/SteamShortcutIdTests.cs`, `.github/workflows/ci.yml`
- Modify: `UWPHook/GamesWindow.xaml.cs` (delete `GenerateSteamGridAppId`, call Core), `Directory.Packages.props` (drop `Crc32.NET`)

**Interfaces:**
- Produces: `public static class SteamShortcutId { public static uint Compute(string exe, string appName); public static int ToVdfAppId(uint id); }`. `ToVdfAppId` is an `unchecked((int)id)`. The `uint` value is the grid file name.

- [ ] **Step 1: Write the failing tests**

```csharp
[Fact] public void Compute_MatchesStandardCrc32WithHighBit() =>
    Assert.Equal(3144859588u, SteamShortcutId.Compute("\"D:\\Program Files\\GH\\GHWT.exe\"", "Guitar Hero World Tour"));
[Fact] public void Compute_IsSensitiveToQuotes() =>
    Assert.NotEqual(SteamShortcutId.Compute("\"C:\\a b\\UWPHook.exe\"", "X"), SteamShortcutId.Compute("C:\\a b\\UWPHook.exe", "X"));
[Fact] public void Compute_UsesUtf8Bytes() =>
    Assert.Equal(Crc32.HashToUInt32(Encoding.UTF8.GetBytes("e太鼓")) | 0x80000000u, SteamShortcutId.Compute("e", "太鼓"));
[Fact] public void ToVdfAppId_IsTwosComplement() => Assert.Equal(-1150107708, SteamShortcutId.ToVdfAppId(3144859588u));
```
(The expected value `3144859588` was computed with Python `zlib.crc32(...) | 0x80000000`.)

- [ ] **Step 2:** Run `dotnet test UWPHook.Core.Tests`. Expected: FAIL (type missing).
- [ ] **Step 3:** Implement with `System.IO.Hashing.Crc32.HashToUInt32(Encoding.UTF8.GetBytes(exe + appName)) | 0x80000000`.
- [ ] **Step 4:** Run the tests again. Expected: 4 passed.
- [ ] **Step 5:** `ci.yml` has two jobs:
  - `core` on `ubuntu-latest`: `dotnet test UWPHook.Core.Tests`.
  - `app` on `windows-latest`: `dotnet build UWPHook.slnx -c Release` + `dotnet publish` for win-x64/win-arm64, and upload artifacts.

  Trigger on push and pull_request.
- [ ] **Step 6:** Commit `feat(core): add Core library, SteamShortcutId, CI`.

### Task 3: Robust launch-argument parsing (fixes #157 #160 #168 for existing shortcuts)

**Files:**
- Create: `UWPHook.Core/Launch/LaunchRequest.cs`, `UWPHook.Core.Tests/Launch/LaunchRequestTests.cs`
- Modify: `UWPHook/AppManager.cs` (`LaunchUWPApp(string[] args)` → `LaunchUWPApp(LaunchRequest request)`), `UWPHook/GamesWindow.xaml.cs` (`LauncherAsync`)

**Interfaces:**
- Produces: `public sealed record LaunchRequest(string Aumid, string Executable, string ExtraArguments) { public static bool TryParse(IReadOnlyList<string> args, out LaunchRequest? request); }`. `args` is the full `Environment.GetCommandLineArgs()`.

- [ ] **Step 1: Write the failing tests**

```csharp
[Fact] public void Parse_StandardArgs() // ["C:\\x\\UWPHook.exe","Microsoft.Forza_8wekyb3d8bbwe!App","forza.exe","-a","b"]
    => Aumid "Microsoft.Forza_8wekyb3d8bbwe!App", Executable "forza.exe", ExtraArguments "-a b"
[Fact] public void Parse_RecoversAumidFromSplitUnquotedExePath() // ["C:\\Program","Files","(x86)\\Briano\\UWPHook\\UWPHook.exe","Microsoft.Forza_8wekyb3d8bbwe!App","forza.exe"]
    => Aumid "Microsoft.Forza_8wekyb3d8bbwe!App", Executable "forza.exe"
[Fact] public void Parse_NormalizesForwardSlashesInExecutable() // "MCC/Binaries/x.exe" → "MCC\\Binaries\\x.exe"
[Fact] public void Parse_MissingExecutable_UsesEmpty()       // [exe, aumid] → Executable ""
[Fact] public void Parse_NoAumidToken_ReturnsFalse()          // [exe] and [exe,"foo"]
```
- [ ] **Step 2:** Run the tests. Expected: FAIL.
- [ ] **Step 3:** Implement. The AUMID is the **first** arg (after index 0) matching `^[^\s!]+_[a-z0-9]{13}![^\s!]+$` (case-insensitive; the PFN publisher hash is 13 chars). The executable is the next arg, and the rest are joined with spaces.
- [ ] **Step 4:** Run the tests. Expected: PASS.
- [ ] **Step 5:** Wire it into `LauncherAsync`. If `TryParse` is false, show "Invalid launch arguments" and log the args. Also handle `IApplicationActivationManager` errors: catch `COMException`/`ArgumentException` and log `HResult` in hex.
- [ ] **Step 6:** Commit `fix(launch): parse AUMID robustly, survive unquoted Steam paths`.

### Task 4: Quoted shortcuts + in-place merge that preserves appid and other tools' entries

**Files:**
- Create: `UWPHook.Core/Steam/ShortcutPaths.cs`, `UWPHook.Core/Steam/ShortcutSpec.cs`, `UWPHook.Core/Steam/ShortcutMerger.cs`, `UWPHook.Core.Tests/Steam/ShortcutMergerTests.cs`
- Modify: `UWPHook/GamesWindow.xaml.cs` (`ExportGames` uses the merger, which maps `VDFEntry` ↔ `ExistingShortcut`)

**Interfaces:**
- Consumes: `SteamShortcutId` (Task 2).
- Produces:
  - `public static class ShortcutPaths { public static string Quote(string path); public static string Unquote(string value); }`
  - `public sealed record ShortcutSpec(string AppName, string Exe, string StartDir, string LaunchOptions, string Icon, IReadOnlyList<string> Tags) { public static ShortcutSpec ForApp(string appName, string aumid, string executable, string uwpHookExePath, string icon, IReadOnlyList<string> tags); }`. `ForApp` quotes `Exe` and `StartDir`.
  - `public sealed record ExistingShortcut(int AppId, string AppName, string Exe, string LaunchOptions);`
  - `public sealed record MergeResult(IReadOnlyList<MergedShortcut> Shortcuts);`, with `public sealed record MergedShortcut(int AppId, ShortcutSpec? Spec, int? ExistingIndex)`. `Spec == null` means keep the existing entry untouched.
  - `public static class ShortcutMerger { public static MergeResult Merge(IReadOnlyList<ExistingShortcut> existing, IReadOnlyList<ShortcutSpec> wanted); }`

- [ ] **Step 1: Write the failing tests**
  - `ForApp_QuotesExeAndStartDir`: the path `C:\Program Files (x86)\Briano\UWPHook\UWPHook.exe` gives Exe `"\"C:\\Program Files (x86)\\Briano\\UWPHook\\UWPHook.exe\""` and StartDir `"\"C:\\Program Files (x86)\\Briano\\UWPHook\""`.
  - `Merge_AddsNewEntry_WithComputedId`: the AppId equals `SteamShortcutId.ToVdfAppId(SteamShortcutId.Compute(spec.Exe, spec.AppName))`.
  - `Merge_UpdatesUnquotedLegacyEntry_KeepingAppId`: an existing entry `{AppId=-123, Exe="C:\\x y\\UWPHook.exe", LaunchOptions="Pfn_abcdefghijklm!App game.exe"}` plus a wanted spec with the same AUMID gives one result with `AppId=-123` and `ExistingIndex=0`.
  - `Merge_UpdatesQuotedEntry_EvenIfUserRenamed`: matching is by AUMID in `LaunchOptions` + an Exe file name of `UWPHook.exe` (case-insensitive, quotes ignored), **not** by AppName.
  - `Merge_LeavesForeignShortcutsUntouched`: an entry with Exe `"C:\\Games\\x.exe"` gives `Spec == null` at the same position.
  - `Merge_DoesNotDuplicateWhenTwoLegacyEntriesMatch`: two existing entries with the same AUMID → keep the first, update it, and keep the second untouched (log it, don't delete).
- [ ] **Step 2:** Run the tests. Expected: FAIL.
- [ ] **Step 3:** Implement (pure functions, no I/O).
- [ ] **Step 4:** Run the tests. Expected: PASS.
- [ ] **Step 5:** In `ExportGames`:
  - Build the specs with `ForApp`, using `Environment.ProcessPath`.
  - Run the merge.
  - For a `MergedShortcut` with a `Spec`, write the `VDFEntry` fields from the spec. For `Spec == null`, copy the original `VDFEntry` object.
  - Reindex `Index` sequentially.
  - Name grid files with the **merged AppId** (`unchecked((uint)AppId)`).
- [ ] **Step 6:** *Manual (Windows):* export a game with 2.14.3, upgrade, and re-export. Steam shows one entry, the art is kept, and it launches from a path with spaces.
- [ ] **Step 7:** Commit `fix(export): quote Exe/StartDir, merge by AUMID, preserve appid`.

### Task 5: Export never hangs or crashes

**Files:**
- Create: `UWPHook.Core/Steam/ISteamProcess.cs`, `UWPHook.Core/Steam/SteamRestarter.cs`, `UWPHook.Core.Tests/Steam/SteamRestarterTests.cs`, `UWPHook/Infrastructure/WindowsSteamProcess.cs`, `UWPHook/Infrastructure/ErrorReporter.cs`
- Modify: `UWPHook/App.xaml.cs` (global handlers), `UWPHook/GamesWindow.xaml.cs` (`ExportButton_Click`, `RestartSteam` removed, `SaveImage`), `UWPHook/AppEntry.cs` (`widestSquareIcon` disposes images)

**Interfaces:**
- Produces:
  - `public interface ISteamProcess { bool IsRunning(); string? ExecutablePath(); void RequestExit(); void Start(string exePath); }`
  - `public enum RestartOutcome { NotRunning, Restarted, TimedOut }`
  - `public sealed class SteamRestarter(ISteamProcess steam, TimeProvider time, TimeSpan timeout, TimeSpan poll) { public Task<RestartOutcome> RestartAsync(CancellationToken ct = default); }`
  - `public static class ErrorReporter { public static void Report(Exception ex, string userMessage); }` (logs + MessageBox on the dispatcher)

- [ ] **Step 1: Write the failing tests** (fake `ISteamProcess` + `Microsoft.Extensions.TimeProvider.Testing.FakeTimeProvider`):
  - `Restart_WhenNotRunning_ReturnsNotRunning_AndDoesNotStart`
  - `Restart_WhenExitsAfterTwoPolls_StartsOnceAndReturnsRestarted`
  - `Restart_GivesUpAfterTimeout` (never exits; timeout 15 s; returns `TimedOut` without calling `Start`)
  - `Restart_WhenExecutablePathUnavailable_ReturnsTimedOutWithoutThrowing`
- [ ] **Step 2:** Run the tests. Expected: FAIL.
- [ ] **Step 3:** Implement `SteamRestarter`. `WindowsSteamProcess` resolves the path from `HKCU\Software\Valve\Steam\SteamExe` first, then `Process.MainModule` in a try/catch. It uses `GetProcessesByName("steam")` with `Any()` (never `SingleOrDefault`) and requests exit via `steam.exe -shutdown`.
- [ ] **Step 4:** Run the tests. Expected: PASS.
- [ ] **Step 5:** Hardening in the app:
  - `ExportButton_Click` wraps everything in `try/catch (Exception)` → `ErrorReporter`, with a `finally` that re-enables the UI. A `TimedOut` outcome shows "Please restart Steam to see your games".
  - `App.xaml.cs` subscribes `DispatcherUnhandledException`, `TaskScheduler.UnobservedTaskException` and `AppDomain.UnhandledException` → `ErrorReporter`.
  - `SaveImage` uses one shared static `HttpClient` and writes the **raw bytes** with the extension from the URL (`.png/.jpg/.webp`) instead of decoding with `Bitmap`.
  - `widestSquareIcon` uses `using var img = Image.FromFile(...)`.
- [ ] **Step 6:** *Manual (Windows):* export with Steam running, with Steam closed, and with the SGDB "animated" filter. All three show a message and the app stays open.
- [ ] **Step 7:** Commit `fix(export): bounded Steam restart, global error handling, raw image save`.

### Task 6: App discovery works on every Windows/PowerShell setup (hotfix path)

**Files:**
- Modify: `UWPHook/ScriptManager.cs` (out-of-process Windows PowerShell 5.1, ported from the Mikelrax fork's approach), `UWPHook/Resources/GetAUMIDScript.ps1` (apply PR #169 + PR #182 changes), `UWPHook/GamesWindow.xaml.cs` (`Bwr_DoWork` drops the `Set-ExecutionPolicy` call), `Directory.Packages.props` (remove `Microsoft.PowerShell.SDK`)

**Interfaces:**
- Produces: `static class ScriptManager { public static string RunScript(string scriptText, TimeSpan timeout); }` (throws `ScriptFailedException(string stderr, int exitCode)` on a non-zero exit).

- [ ] **Step 1:** Cherry-pick PR #169 (`ms-resource` name → fallback `$name = $app.Name`) and PR #182 (dedupe by name + package family) into the script, with author credit in the commit message (`Co-authored-by`).
- [ ] **Step 2:** Implement `RunScript`:
  - Write the script to `%TEMP%\UWPHook_<guid>.ps1` as UTF-8 with a BOM and append `| Out-String` to the last line.
  - Run `%SystemRoot%\System32\WindowsPowerShell\v1.0\powershell.exe -NoProfile -NonInteractive -ExecutionPolicy Bypass -File <tmp>`.
  - Read stdout/stderr asynchronously (to avoid a deadlock), apply the timeout, and delete the temp file in `finally`.
  - Use it for `Set-WinUILanguageOverride` too.
- [ ] **Step 3:** Remove the "Change resolution" toggle and combo from `SettingsWindow` (+ settings keys); it comes back in Phase 3. Make `saveButton_Click` null-safe (`SelectedItem as string ?? current`, and seconds from `SelectedIndex`).
- [ ] **Step 4:** Verify:
  - Run `dotnet build -c Release`. Expected: success, and the publish output no longer contains `System.Management.Automation.dll`.
  - *Manual (Windows):* (a) the app list loads with PowerShell 7 installed and set as default; (b) Forza Motorsport, or any app with an `ms-resource:` name, appears; (c) changing settings and saving doesn't crash.
- [ ] **Step 5:** Commit `fix(discovery): run Windows PowerShell 5.1 out-of-process; merge #169 #182; settings save crash`.

### Task 7: Release 2.15

**Files:**
- Create: `.github/workflows/release.yml`, `CHANGELOG.md`
- Modify: `README.md` (building, troubleshooting for spaces/Steam restart, backups), `Directory.Build.props` (version)
- Create: `UWPHook/Program.cs`, `UWPHook.Core/Install/InstallLayout.cs`, `UWPHook.Core.Tests/Install/InstallLayoutTests.cs`
- Delete: `UWPHook.nsi` (replaced by Velopack, decision 2)

- [ ] **Step 0 (TDD):** `public static class InstallLayout { public static string StableExePath(string processPath); }`. Per ruling R24 it returns the running exe path unchanged (Velopack `current\` is already update-stable); it stays the single seam for this decision. Tests:
  - `StableExePath_UnderVelopackRoot_ReturnsTheExeInsideCurrent`
  - `StableExePath_PortableZip_ReturnsSamePath`

  Use it wherever `ShortcutSpec.ForApp` gets the UWPHook path.
- [ ] **Step 1:** Add Velopack:
  - Add the `Velopack` package and a custom `Program.Main` (`<StartupObject>UWPHook.Program</StartupObject>`, `[STAThread]`) whose first line is `VelopackApp.Build().Run()`.
  - `release.yml` runs on tag `v*`: `dotnet publish` for win-x64 and win-arm64, then `vpk pack --packId UWPHook --packVersion <tag> --runtime <rid>`.
  - Upload `Setup.exe`, the portable zip and the release feed to a GitHub Release with SHA-256 sums.
  - The install is per-user (decision 3).
- [ ] **Step 2:** `CHANGELOG.md` 2.15.0 lists the fixes with issue numbers: #127 #139 #141 #149 #155 #157 #160 #162 #164 #166 #168 #171 #176 #178 #181, plus PRs #169 #182.
- [ ] **Step 3:** *Manual:* run the user acceptance checklist on Windows 11 25H2 + Windows 10 22H2: list, export, launch from Steam, re-export, stream mode.
- [ ] **Step 4:** Commit `chore(release): 2.15.0`, tag `v2.15.0` (the user pushes the tag), and close the triaged issues and PRs per report §4–§5.

---

## Phase 2 — 3.0 core (plan each task in detail when starting it)

The interfaces are fixed here so that later tasks agree. Each task gets the same TDD step structure as Phase 1.

### Task 8: `IAppCatalog` via PackageManager (replaces PowerShell)
- Core: `public sealed record XboxApp(string Aumid, string PackageFamilyName, string DisplayName, string Executable, string? LogoPath, string InstallLocation, DateTimeOffset InstalledDate);` and `public interface IAppCatalog { Task<IReadOnlyList<XboxApp>> GetInstalledAppsAsync(bool includeSystem, CancellationToken ct); }`.
- Core: `MicrosoftGameConfig.TryReadExecutable(string xml, out string exe)` and `AppxManifest.ReadApplications(string xml)`. These are pure XML parsers, tested with fixture files (Halo MCC multi-exe, GameLaunchHelper, Forza `ms-resource`).
- App: `PackageManagerCatalog` uses `new PackageManager().FindPackagesForUser("")` and skips `IsFramework`/`IsResourcePackage`. For each `GetAppListEntries()` it takes `AppUserModelId` and `DisplayInfo.DisplayName`, uses the manifest for the executable, and dedupes by AUMID (not name). `KnownApps.json` stays as a name override.
- Then delete `ScriptManager`'s discovery use and `GetAUMIDScript.ps1`.
- Covers: §5 🟠 missing-apps cluster.

### Task 9: `ShortcutsFile` on ValveKeyValue (replaces VDFParser)
- Core: `public sealed class ShortcutsFile { public static ShortcutsFile Load(Stream s); public IReadOnlyList<ExistingShortcut> Entries { get; } public void Apply(MergeResult merge); public void Save(Stream s); }`. It keeps each entry's full `KVObject`, so unknown keys round-trip.
- Tests: copy `shortcuts.vdf`, `shortcuts_utf8.vdf` and `shortcuts4.vdf` from BrianLima/VDFParser `VDFParserTests/Resources`. Test cases: `RoundTrip_IsByteIdenticalWhenUnchanged`, `RoundTrip_PreservesUnknownKeys`, `RoundTrip_PreservesUtf8Names`, `Load_DuplicateKeys_DoesNotThrow`, `Load_EmptyFile_ReturnsNoEntries`.
- Then delete the `VDFParser/` project.

### Task 10: Split `GamesWindow` (Sandi Metz refactor, behaviour-preserving)
- Core services:
  - `SteamLibrary` (install path from HKCU `SteamPath`, then HKLM; users from `config/loginusers.vdf` + `userdata`, behind `ISteamLocator`)
  - `ShortcutExporter` (orchestrates backup → load → merge → save → artwork)
  - `ShortcutBackup`
  - `ArtworkDownloader` (SGDB client: URL-encoded search, one `HttpClient`, 401 → `InvalidApiKey` result without wiping the key)
  - `IconResolver`
- App: MVVM with CommunityToolkit.Mvvm (`GamesViewModel`, `SettingsViewModel`) and JSON settings in `%AppData%\Briano\UWPHook\settings.json`, with the SGDB key DPAPI-protected and a one-time import from old `user.config`.
- Rule: no class over ~150 lines; `GamesWindow.xaml.cs` only wires the view.

### Task 11: Headless launcher + package-aware process tracking
- `App.OnStartup`: if `LaunchRequest.TryParse` succeeds, run `GameSession` **without creating `GamesWindow`**. The stream splash is optional and uses `RenderOptions.ProcessRenderMode = SoftwareOnly` (#165).
- Core: `public sealed class GameSession(IAppActivator activator, IPackageProcessWatcher watcher, TimeProvider time)`, which waits until no process of the package family is alive for N seconds (grace period for launchers like MCC/FH5/Fallout 76).
- App: `PackageProcessWatcher` uses `GetPackageFamilyName(OpenProcess(...))` + a fallback "image path under InstallLocation".
- Covers: #59 #58 #108 #117 #135 #165.

### Task 12: Fluent theme, drop MaterialDesign (decision 1)
- Set `ThemeMode="System"` in `App.xaml`, and remove the MaterialDesign resource dictionaries and the `MaterialDesignColors`/`MaterialDesignThemes` packages.
- Replace `materialDesign:PackIcon` with `Segoe Fluent Icons` glyphs (Settings `U+E713`, Refresh `U+E72C`).
- Replace `ColorZone`/`Chip`/toggle styles with stock Fluent controls.
- Replace `FullScreenLauncher`'s `PaletteHelper` with `ThemeMode="Dark"`.
- Do it after Task 10, so the XAML is rewritten once.
- Tests: Task 10 ViewModel tests stay green.
- *Manual (Win10 + Win11):* light/dark follow the system, and there are no missing-resource exceptions.

### Task 13: Platform capability gating + zero PowerShell (API audit, 2026-11-08)
Audit result: no API UWPHook uses is deprecated on Windows 11. `IApplicationActivationManager`, `PackageManager.FindPackagesForUser` and `AppListEntry` are all current. GetAUMID is already pure C# (Task 8b). PowerShell remains only for `Set-WinUILanguageOverride`, and Microsoft documents that cmdlet as needing a sign-out to take effect, so a per-launch language switch cannot work.
- Core: add `PlatformCapabilities`, a record of feature flags computed from an `IOsVersion` seam, with unit tests for 1809, 1903, 2004 and 11. App code asks the capability, never the build number.
- Package paths: when the OS is 18362 or later (`OperatingSystem.IsWindowsVersionAtLeast(10,0,18362)`), prefer `Package.EffectiveLocation` over `InstalledLocation`. It returns the mutable/ModifiableWindowsApps folder for moddable games. Do this in `PackageManagerCatalog` (exe resolution) and in `PackageProcessWatcher` (image-path fallback). Also add the XboxGames flat-install root to the fallback test cases.
- `ComActivator`: create `ApplicationActivationManager` with `CoCreateInstance(..., CLSCTX_LOCAL_SERVER, ...)`, as the docs recommend for a process spawned only to launch an app.
- Language override (decided 2026-11-09 by Brian: **remove**; ruling R37):
  - Remove the "change language" setting and the per-launch override, along with `ScriptManager`, `ScriptManagerTests` and the PowerShell dependency.
  - The 10b settings import drops the legacy keys.
  - CHANGELOG: a "Removed" entry with the reason.
  - This supersedes wave-2 item I1.
  - Never change the user's Windows display language while removing it. A user may still have an override that I1 left behind; the README explains how to clear it in Settings > Time & language.
  - 10c-2 already removes the language controls from Settings.
- Gate any remaining OS-specific UI on `PlatformCapabilities` (hide, don't fail).
- *Manual:* a moddable GDK game (in XboxGames) is tracked until exit, launch on Win10 1809 still works, and no `powershell.exe` is spawned at any point (Process Monitor).

---

## Phase 3 — Features (plan separately, in this order)
1. **Remove UWPHook shortcuts** (replaces "Clear all", D6) + **Steam user picker** (#76).
2. **Sync mode + CLI**: `UWPHook.exe sync [--remove-missing] [--user <id>]`, also a Windows Scheduled Task option (#64).
3. **Artwork picker** (from the dos-ise `SelectImagesWindow` draft) + per-game artwork override.
4. **Launch mode per game**: Activate (default) / direct exe / URI.
5. **Steam collection auto-assign** ("Xbox") with a backup of the cloud-storage json (see the acabtr fork).
6. **Sunshine/Apollo `apps.json` export** (replaces PR #85): only touch own entries, back up before writing.
7. **Resolution switch** reimplemented via `ChangeDisplaySettingsEx` and restored on game exit (#124).
8. In-app updates via Velopack `UpdateManager` + GitHub source; install-date sort (#61); Xbox app pre-launch option (#161).

## Phase 4 — Release engineering
- Code signing: deferred (decision 4). SignPath Foundation is the candidate (#137). Also a winget manifest that points at the Velopack `Setup.exe`.
- Issue templates (bug report asks for `application.log`, Windows build, PowerShell default), labels `streaming`, `missing-game`, `needs-retest`.
- Delete the stale branches `SteamWorksNet`, `UWPTest`, `feature/joystick-binding`.

## Self-review notes
- Every 🔴 cluster in report §5 maps to Tasks 3–6. 🟠 maps to Task 8. Features map to Phase 3. CI and release map to Tasks 2, 7 and Phase 4.
- Type names are consistent across tasks: `SteamShortcutId`, `ShortcutSpec`, `ExistingShortcut`, `MergeResult`/`MergedShortcut`, `LaunchRequest`, `SteamRestarter`/`ISteamProcess`, `XboxApp`/`IAppCatalog`, `ShortcutsFile`.
- WPF/COM/registry code can't be unit-tested on Linux, so each such task has explicit manual Windows checks.
