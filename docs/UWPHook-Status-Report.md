# UWPHook — Status Report (November 2026)

Repo: https://github.com/BrianLima/UWPHook · Analysed at `master` = `109b9b5` (= tag `2.14.3`, 2024-12-08)

## 1. Executive summary

- **The project is dormant, but people still use it a lot.** It has about 2,040 stars and 136 forks. The latest release, 2.14.3, has had about 138k `.exe` downloads. It has 75 open issues and 5 open PRs. The last commit was in Dec 2024, and several forks are now shipping their own fixes.
- **The latest release (2.14.3) is the most broken one.** The .NET 8 port by `dos-ise` caused three regressions that account for most new issues:
  1. **The hosted PowerShell 7 SDK can't load the `Appx` module** (`0x80131539`). The app list is empty or shows "Error trying to get installed apps". Issues: #132 #141 #149 #155 #157 #164 #171 #178 #181.
  2. **The Steam shortcut `Exe` is no longer quoted** (commit `76601e4`). If UWPHook lives in a path with spaces, Steam's args are split and `ActivateApplication` gets garbage, giving *"Value does not fall within the expected range"*. Issues: #157 #160 #168. Changing `Exe` also changes the CRC-derived appid, so existing users get **duplicate shortcuts and orphaned artwork**.
  3. **"Change resolution" doesn't work.** `Set-DisplayResolution` doesn't exist on client Windows, the call has a typo (`- Height`), and the combo is often empty, so the settings window crashes on Save. Issues: #124 #162.
- **Export crashes ("app just closes") come from unhandled exceptions in `async void` handlers.** The worst one is an **infinite loop in `RestartSteam`**: `while (!restarted || elapsed < 8)` never exits if Steam refuses to quit. Users found the workaround "close Steam first". Issues: #166 #176 #80 #143 #158.
- **.NET 8 reaches end of support on 2026-11-10, two days from now.** Retarget to **.NET 10 LTS** (supported to Nov 2028) and build with **Visual Studio 2026**.
- **Main recommendation:** ship a quick **2.15 "stabilize" release** with small, surgical fixes. In parallel, do a **3.0 rewrite of the core**, keeping the WPF UI:
  - Replace PowerShell with the WinRT `PackageManager` API.
  - Replace the hand-rolled VDF parser with `ValveKeyValue`.
  - Split the 886-line `GamesWindow` god class into small testable objects.
  - Add CI and signed releases.

## 2. Codebase inventory

| Area | State | Notes |
|---|---|---|
| Target | `net8.0-windows`, WPF, `PublishSingleFile`, `win-x64`, `IncludeAllContentForSelfExtract` | Self-extract unpacks to `%TEMP%`, which slows startup and is a likely cause of AV false positives (#137). |
| Solution | `UWPHook.sln` from VS 2017 (format 12, VS 15), one project, no tests | Nothing to run in CI. |
| Size | ~1,900 LOC C# + XAML; `GamesWindow.xaml.cs` = 886 LOC | God class: UI, logging bootstrap, launcher, Steam I/O, HTTP, imaging, backups. |
| Build reproducibility | ❌ **Broken for anyone but the author** | `VDFParser.dll` comes from `..\..\SharpSteam\...\bin\Release` **and** `..\..\VDFParser\...\bin\Debug` (two conflicting HintPaths). Those libs are **.NET Framework 4.8** old-style projects. README still says ".NET Framework 4.8". SharpSteam isn't used in code at all. |
| Leftovers from .NET Framework | `App.config` (binding redirects, `supportedRuntime 4.8.1`), `Properties/AssemblyInfo.cs`, `Settings.settings` | Ignored or legacy under .NET (Core). `Microsoft.AspNet.WebApi.Client` is used only for `ReadAsAsync`, and `System.Memory` / `System.Security.Principal.Windows` packages are unnecessary. |
| Installer | `UWPHook.nsi` with hard-coded `C:\Users\Brian\...` paths | Lists **.NET Framework-era DLLs** (Newtonsoft.Json.Bson, System.Buffers…), so it's stale for the .NET 8 build. 2.14.3 shipped exe+zip only. |
| CI/CD | None | No GitHub Actions, no signing, no winget. |
| Stale branches | `SteamWorksNet` (2018), `UWPTest` (2017), `feature/joystick-binding` (2017) | Safe to delete or archive. |

### 2.1 How it works (to keep in mind)

1. **Discovery:** an embedded `GetAUMIDScript.ps1` runs through the PowerShell SDK runspace. It calls `Get-AppxPackage`, reads each manifest (plus `MicrosoftGame.Config` for GDK games), and returns `name|logo|PFN!AppId|exe;` strings.
2. **Export:** for each Steam user folder it does the following:
   - Parses `shortcuts.vdf` with BrianLima/VDFParser.
   - Adds or replaces entries where `Exe = UWPHook.exe` and `LaunchOptions = "<AUMID> <exe>"`. The appid is `crc32(exe+name) | 0x80000000`.
   - Backs up the vdf, then writes it.
   - Downloads SteamGridDB art into `userdata/<id>/config/grid/{appid}{,p,_hero,_logo}.png`.
   - Restarts Steam.
3. **Launch:** Steam runs `UWPHook.exe <AUMID> <exe> [args]`, and UWPHook then:
   - Starts the app with COM `IApplicationActivationManager.ActivateApplication`.
   - Polls the PID, or finds the child process by exe name or path through WMI.
   - Exits when the game exits, so Steam shows "playing" and streaming works.

### 2.2 Defects found in code review (beyond the regressions above)

| # | Where | Problem | Impact |
|---|---|---|---|
| D1 | `GamesWindow.RestartSteam` | `while (!restarted \|\| t<8)` never ends if Steam doesn't exit. `SingleOrDefault()` throws if >1 `steam` process. `MainModule` can throw. | Hang / crash on export (#166, #176) |
| D2 | `ExportButton_Click` (`async void`) | Only `TaskCanceledException` is caught. Any `HttpRequestException`, `IOException` or `ArgumentException` kills the process. No `DispatcherUnhandledException` handler exists. | Silent "app closes" |
| D3 | `SaveImage` | `new Bitmap(stream)` re-encodes every download as PNG and **throws on WebP/animated** assets (the "animated" filter, which SGDB often returns as WebP). Uses obsolete `WebClient`, and a new `HttpClient` per API instance. | Crash when animated art is selected |
| D4 | `AppEntry.widestSquareIcon` | `Image.FromFile` is never disposed, so files stay **locked**. That's why `PersistAppIcon` hits `IOException` and the "backup icon" path exists. | Wrong or missing icons, leaks |
| D5 | VDFParser state machine | Heuristic (`0x08 0x08` terminator, 3-byte int lookahead). Uses `Dictionary.Add`, so a duplicate key throws *"key already exists"* (#139). Maps to a fixed model and **silently drops unknown fields** (`sortas`, `FlatpakAppID`, `DevkitOverrideAppID`…) of *every* shortcut, including other tools' shortcuts. | Data loss in other apps' shortcuts, failed exports |
| D6 | `ClearAllShortcuts` | Writes an empty `shortcuts.vdf` for **every** Steam user, wiping all non-Steam games from every tool. | Destructive feature |
| D7 | `ExportGames` | Writes to **every** folder in `userdata` (including stale or family accounts, #76). Steam path is read only from HKLM; HKCU `SteamPath` is ignored. | Clutter, wrong account |
| D8 | Launcher mode | Launching a game builds the full main `GamesWindow` (WPF + GPU), then `Thread.Sleep` polls **on the UI thread**. Keeping a WPF GPU process alive blocks NVIDIA Advanced Optimus MUX switching (#165). | Optimus incompatibility, frozen window in stream mode |
| D9 | `SettingsWindow.saveButton_Click` | `SelectedItem.ToString()` on possibly-null combos (resolution, culture). `Substring(0,1)` parse of "N seconds". | Crash on Save (#162) |
| D10 | `SteamGridDbApi.SearchGame` | Game name not URL-encoded (`/ ? # &` break the call). Blindly takes `games[0]`. On 401 it **erases the user's API key**. | Wrong or missing art |
| D11 | `IsRunning` | Process match by `Name` uses a dict keyed by process name (duplicates dropped). Matching with `Contains` can latch onto an unrelated process. | Steam "stops playing" early or never |
| D12 | `Set-WinUILanguageOverride` via PS7 runspace | Same Appx/International module compatibility risk. | Language switch silently fails |
| D13 | Settings & secrets | `Properties.Settings` user.config path is keyed on exe path and version, so settings reset on update. The SGDB API key is stored in plain text. | UX, minor security |

## 3. Upgrade path (VS / .NET)

| From | To | Why / how |
|---|---|---|
| .NET 8 (EOL **2026-11-10**) | **.NET 10 LTS** (to Nov 2028), TFM `net10.0-windows10.0.19041.0` | The Windows SDK TFM gives CsWinRT projections (`Windows.Management.Deployment.PackageManager`, `Windows.ApplicationModel.AppListEntry`) with no extra package. Keep `SupportedOSPlatformVersion=10.0.17763.0`. |
| VS 2017 `.sln` | **Visual Studio 2026** + `.slnx` | VS 2026 is the IDE that officially targets .NET 10. Use `dotnet sln migrate`. |
| Per-project versions | `Directory.Build.props` + `Directory.Packages.props` (Central Package Management) | One place for LangVersion, Nullable, TreatWarningsAsErrors, version. |
| `AssemblyInfo.cs`, `App.config`, `Settings.settings` | csproj `<Version>`, delete App.config, JSON settings in `%AppData%\Briano\UWPHook\settings.json` | Settings survive updates. Protect the SGDB key with DPAPI (`ProtectedData`). |
| Microsoft.PowerShell.SDK 7.4 + S.M.Automation (~70 MB, the root of the Appx bug) | **Remove**. Use `PackageManager.FindPackagesForUser("")` + `Package.GetAppListEntries()` + manifest / `MicrosoftGame.Config` XML parsing in C#. | Fixes the whole "Appx" issue cluster, shrinks the binary a lot, and needs no execution policy hacks. |
| BrianLima/VDFParser (net48 DLL via HintPath) | **ValveKeyValue** NuGet (`KVSerializationFormat.KeyValues1Binary`) behind a small `ShortcutsFile` wrapper that keeps unknown keys | Reproducible build, round-trip fidelity. |
| `Microsoft.AspNet.WebApi.Client`, `WebClient`, `Newtonsoft.Json` | `System.Net.Http.Json` + `System.Text.Json`, a single shared `HttpClient` | Fewer deps, no obsolete APIs. |
| `Crc32.NET` | `System.IO.Hashing.Crc32` (first-party) | Same algorithm, maintained. |
| MaterialDesignThemes 5 | Keep for 3.0, or move to WPF's built-in Fluent theme (`ThemeMode`, .NET 9+) | Decision for you (see §7). |
| Single-file self-extract | `PublishSingleFile` **without** `IncludeAllContentForSelfExtract`, plus `EnableCompressionInSingleFile`. Ship `win-x64` **and `win-arm64`**. | Faster start, fewer AV flags. Note that `Assembly.Location` is empty, so use `Environment.ProcessPath`. |
| NSIS with absolute paths | GitHub Actions: build, test, publish, zip (+ optional Inno Setup/Velopack), sign via **SignPath Foundation** (free for OSS), winget manifest | Reproducible, signed releases. |

## 4. Open PR triage (5)

| PR | Verdict | Reason |
|---|---|---|
| **#169** "[127] Fix Forza not showing" (icavalheiro, 2025-07) | ✅ **Merge into 2.15** | 1-line script change: fall back to `$app.Name` when DisplayName is an `ms-resource:` string. Safe. In 3.0 this becomes proper resource resolution via `AppListEntry.DisplayInfo`. |
| **#182** "Improve duplicate file logic" (RXA7, 2026-08) | ✅ **Merge into 2.15** | Dedupe on name **+ package family** (Hollow Knight vs Silksong). Port the rule to C# in 3.0. |
| **#163** "PowerShell Core fix" (deathscore13, 2025-03) | 🟡 **Cherry-pick the idea, close the PR** | `Import-Module Appx -UseWindowsPowerShell` is the same fix GeorgeBroughton's fork shipped (mixed results in #155/#157). The nullable churn plus a blanket `<NoWarn>` hides real bugs and conflicts with the refactor. |
| **#172** "Run Appx under PowerShell 5.x" (theCrius, 2025-07) | ❌ **Close** | Relies on `$PSCommandPath`, which is **empty** when the script runs from a string in a hosted runspace, so it can't work inside UWPHook. The out-of-process approach in the **Mikelrax fork** (`ScriptManager` spawns `powershell.exe -File tmp.ps1`) is the correct version for the 2.15 hotfix. |
| **#85** "NVIDIA Shield streaming support" (2022) | ❌ **Close (thank the author)** | NVIDIA GameStream is discontinued. Re-target the idea as a **Sunshine/Apollo `apps.json` export** (see §6). |

### Forks worth harvesting

| Fork | What to take |
|---|---|
| **Mikelrax** (2026-06) | Force Windows PowerShell 5.1 out-of-process and drop the PS SDK. **Best 2.15 hotfix base.** |
| **acabtr** (2026-08, "maintained fork") | VDFParser from NuGet, modern **Steam collections** support, "sync Xbox games & remove stale shortcuts", URL-encoded SGDB search. Ideas, not code (single 554-line diff in `GamesWindow`). |
| **dos-ise** (2026-01) | .NET 10 retarget. `SelectImagesWindow` draft (pick art manually). |
| **horgeon** (2025-06) | ARM64 build, relative NSIS paths, publish profile. |
| **konek101** (2025-07) | Use the real app icon instead of the logo. |
| **GeorgeBroughton** (2025-01) | `-UseWindowsPowerShell` fix, already covered. |
| **wesleycariolano** (2026-10) | Large rebrand ("sGs-app"), unified import flow, artwork picker. Inspiration only. |

## 5. Open issue triage (75)

Legend: **🔴 Fix (root cause known)** · **🟠 Fix via 3.0 rewrite** · **🟡 Feature backlog** · **🔵 Streaming (best effort / docs)** · **⚪ Close**

### 🔴 Fix in 2.15 (root cause identified)

| Cluster | Issues | Root cause → fix |
|---|---|---|
| Appx / PowerShell 7 can't list apps | #132 #141 #149 #155 #157 #164 #171 #178 #181 | Hosted PS7 runspace can't load Appx → run `powershell.exe` 5.1 out-of-process (2.15). Use `PackageManager` (3.0). |
| "Value does not fall within the expected range" / path with spaces | #157 #160 #168 (#170 likely) | Unquoted `Exe`/`StartDir` → quote both. Migrate existing entries by matching on AUMID in `LaunchOptions`, **not** on `Exe` string equality. |
| Export closes / hangs | #166 #176 #80 #143 #158 | D1 + D2 + D3 → bounded `RestartSteam`, try/catch in handlers, global exception handler with log + message, write raw image bytes. |
| Settings crash on Save | #162 (#124) | D9 → null-safe combos. Remove the resolution feature in 2.15 and re-add it in 3.0 with `ChangeDisplaySettingsEx` + restore on exit. |
| "Key already exists" | #139 | D5 → tolerant parser in 2.15 (catch, keep the file, tell the user). ValveKeyValue in 3.0. |
| Missing `shortcuts.vdf` | #138 | Probably already fixed in 2.14.x (`ReadShortcuts` returns empty). Add a test and close. |

### 🟠 Missing / misdetected apps → fixed by the PackageManager catalog (3.0)

#179 (FH6, partial), #174 (Xbox app), #159 (Spotify), #156 (Astroneer), #146 #142 #110 (Gears UE), #144 (Starfield), #136 (ACA NEOGEO), #130 #126 (Asphalt), #128 (Sea of Thieves), #127 (Forza Motorsport), #118 (AoE IV editor picked as game), #83 (several), #53 (games on other drives), #45 (MSFS).
Root causes: dropping apps whose DisplayName is `ms-resource:`, "first match wins" dedupe, multi-app packages, GDK `MicrosoftGame.Config` executable lists, framework/system filter.
3.0 approach:
- One entry per `AppListEntry`, with resolved display names and real logos.
- A "show hidden/system apps" toggle.
- A **manual "Add by AUMID"** escape hatch.
- Keep `KnownApps.json` only as a name override list.

### 🟡 Feature backlog (worth doing)

| Issue | Idea |
|---|---|
| #64 | **Sync / automatic mode** + CLI (`UWPHook.exe sync`): add new, update changed, remove uninstalled UWPHook shortcuts. |
| #61 | Sort by install date (newest first). |
| #161 | Option: start the Xbox app or Gaming Services before launch (multiplayer sign-in). |
| #165 | Lightweight headless launcher (no WPF window in launch mode, or software rendering) for Advanced Optimus. |
| #59 #58 #117 #108 #135 #119 | Better process tracking: watch **all processes belonging to the package** (`GetPackageFamilyName` on the process handle, or image path under `InstallLocation`) instead of PID + name `Contains`. Pass Steam launch options through correctly. |
| #137 | Code signing + no self-extract to reduce AV flags. |
| #63 #103 | Icons: real app icon (konek101), persisted under `%AppData%`. Partly done. |
| #76 | Per-Steam-user selection. "Remove UWPHook shortcuts" instead of "clear all". |

### 🔵 Streaming-related (triage, document, best effort)

#177 #145 #135 #74 #60 #37 #32 #23 #27 #17 #5 #34. Most are Steam Remote Play / Windows focus limitations. Actions:
- Rework "stream mode" (no full-screen WPF splash; restore focus using the package process).
- Add a troubleshooting page.
- Offer the **Sunshine/Apollo export** as the modern streaming path.

### ⚪ Close (with a short comment)

| Issue | Reason |
|---|---|
| #175 "Is the project dead?" | Answer with the roadmap and the 2.15 release. |
| #173 Google Play Games | Out of scope. Possibly later via a generic "custom URI launcher". |
| #82 Install XGP games through Steam | Not possible. |
| #78 EA/EGS support | Out of scope; point to BoilR/SRM. |
| #111 #123 | Question / insufficient info. |
| #15 #34 | GeForce Experience / Shield GameStream discontinued. |
| #19 #39 #92 #107 #28 | 2017–2022 reports against old code. Close after 2.15 ships with "please re-test on 2.15". |

## 6. Similar projects: what to learn

| Project | Status | Relevant ideas |
|---|---|---|
| **Steam ROM Manager** (SteamGridDB) | Active | Has a UWP/Xbox parser with two launch modes: "via UWP" and "via executable". Artwork-only parsers for existing shortcuts. Controller templates. Full CLI. |
| **BoilR** (Rust) | Maintained, slower lately | Lists Game Pass as a source. Instant sync, ~3 MB binary, backups, rename, SGDB art auto + manual pick. |
| **steamsync** (Python) | Low activity | Xbox games launched via `explorer shell:AppsFolder\AUMID`, which has no tracking. That is UWPHook's differentiator. |
| **GlosSI** | Superseded/unmaintained | Steam Input + overlay for UWP via a transparent target window. Do **not** replicate. Possibly interop later. |
| **Playnite + Sunshine/Apollo exporters** (SunshineAppExport, ApolloSync, Sunrise, Helios) | Active | Sync to Sunshine/Apollo `apps.json`: non-destructive (only touch own entries), backup before write, restart service, covers. |
| **acabtr fork** | 2026 | Steam collections (cloud-storage json): auto-put exported games in an "Xbox" collection. |

**Feature ideas ranked by value / effort:**
1. **Sync mode + CLI** (add/update/remove). High value, medium effort.
2. **Non-destructive shortcut management:** track UWPHook-owned shortcuts and offer "Remove UWPHook shortcuts" instead of "Clear all". High value, low effort.
3. **Artwork picker** (dos-ise draft, SRM/BoilR style): choose among SGDB results, keep the original format. Medium/medium.
4. **Launch mode per game:** `ActivateApplication` (default), direct exe (for "advanced management"/modding-enabled Game Pass installs, gives native Steam overlay/input), protocol URI. Medium/medium.
5. **Steam collection "Xbox / Game Pass"** auto-assignment. Medium/medium (undocumented format, so needs a backup).
6. **Sunshine/Apollo export** (replaces PR #85). Medium/low.
7. **Steam user picker** (from `loginusers.vdf`). Low effort.
8. **Auto-update check** against GitHub releases. Low effort.

## 7. Decisions needed from you

1. **UI stack for 3.0:** keep MaterialDesign (least churn) or switch to WPF's built-in Fluent theme (fewer deps, native Win11 look). *Recommendation: keep MaterialDesign through 3.0 and revisit later.*
2. **Installer:** zip + winget only, or keep an installer (Inno Setup / Velopack with auto-update)? *Recommendation: Velopack.*
3. **Default install location:** per-user `%LocalAppData%\Programs\UWPHook` (no spaces risk, no admin).
4. **Code signing:** apply to SignPath Foundation (needs an OSI license, which MIT is, plus a public repo with CI).
5. **2.15 hotfix:** do it (recommended, since 138k users are on a broken build) or go straight to 3.0.

## Sources

- .NET 8/9 end of support: https://devblogs.microsoft.com/dotnet/dotnet-8-9-end-of-support/
- VS 2026 + .NET 10 GA: https://www.neowin.net/news/microsoft-launches-visual-studio-2026-and-net-10-with-big-performance-improvements/
- PS7 Appx incompatibility (0x80131539): https://learn.microsoft.com/zh-cn/answers/questions/1665679/poweshell7-get-appxpackage
- ValveKeyValue: https://www.nuget.org/packages/ValveKeyValue
- Steam ROM Manager: https://raw.githubusercontent.com/SteamGridDB/steam-rom-manager/master/README.md
- BoilR: https://awesome.ecosyste.ms/projects/github.com%2FPhilipK%2FBoilR
- GlosSI: https://github.com/Alia5/GlosSI
- steamsync changelog: https://data.safetycli.com/changelogs/steamsync
- ApolloSync (Playnite): https://gh.r-mj.com/sharkusmanch/playnite-apollo-sync
- Repo data (issues, PRs, releases, forks) pulled from the GitHub API and `git` on 2026-11-08.
