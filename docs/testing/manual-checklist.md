# Manual Windows checklist

Checks that need a real Windows machine (they cannot run in CI). Tick them off on a Windows box before a release.

## Task 1
- [ ] The published `UWPHook.exe` (CI artifact `UWPHook-win-x64`) opens, and Settings shows the version `2.15.0.0`.
- [ ] The publish folder contains a single-file `UWPHook.exe` plus native runtime DLLs in the same folder (no `.pdb` files), and running it does not extract anything to `%TEMP%` (no new `.net\UWPHook` folder under `%TEMP%`).
- [ ] `%AppData%\Briano\UWPHook\application.log` is still written after launching the app.

## Task 2
- [ ] Re-exporting a game that UWPHook added with 2.14 gives the same `appid` in `shortcuts.vdf`, and its SteamGridDB grid/hero/logo images still show in Steam (the id switched from Crc32.NET to `SteamShortcutId`).

## Task 3
- [ ] A Steam shortcut whose `Exe` is UWPHook's path **without quotes** (in a folder with spaces, e.g. a 2.15 portable copy in `D:\My Games\UWPHook\UWPHook.exe`; 2.14's installer used `%AppData%\Briano\UWPHook`) launches the game instead of failing with "Value does not fall within the expected range". A shortcut made by 2.14 keeps launching the 2.14 exe it names until it is exported again from 2.15.
- [ ] A shortcut whose launch options use forward slashes in the executable (e.g. Halo MCC `MCC/Binaries/...`) launches, and UWPHook keeps running until the game exits.
- [ ] Launching UWPHook with a junk argument (e.g. `UWPHook.exe foo`) shows "Invalid launch arguments", and `application.log` lists the arguments.
- [ ] Launching a shortcut for an uninstalled app shows the "Error while trying to launch `<AUMID>`." message, and `application.log` has a line with `HRESULT 0x........`.
- [ ] AUMID-only shortcut (no executable arg) launches, and UWPHook exits when the game's original process exits, without latching onto another process.

## Task 6
- [ ] With PowerShell 7 installed and set as the default shell, the app list loads (since Task 8b discovery uses the WinRT `PackageManager`, not PowerShell).
- [ ] Forza Motorsport, or any app whose manifest name is an `ms-resource:` reference, appears in the list (PR #169).
- [ ] Two different games whose names share a prefix (e.g. "Hollow Knight" and "Hollow Knight: Silksong") both appear (PR #182).
- [ ] Changing settings and pressing Save doesn't crash; the seconds value round-trips after reopening Settings.
- [ ] The Settings window no longer shows the "Change host resolution" toggle/combo.
- [x] ~~With "Set system language when launching an app" on, launching a game switches the UI language and it is restored after the game closes.~~ *(superseded: language override removed in Task 13, R37)*
- [ ] No console window flashes while the app list loads (the `PackageManager` catalog starts no process).

## Task 4
- [ ] Export a game with UWPHook 2.14.3, upgrade to 2.15, and re-export it: Steam shows exactly one entry for the game (same `appid` in `shortcuts.vdf`), its SteamGridDB grid/hero/logo art is kept, and it launches when UWPHook is installed in a path with spaces (e.g. `C:\Program Files (x86)\...`).
- [ ] In `shortcuts.vdf` after the export, the UWPHook entry has `Exe` and `StartDir` wrapped in double quotes, and shortcuts added by other tools (or by hand) are unchanged.
- [ ] With a SteamGridDB key set, exporting a new game puts its images in `userdata/<id>/config/grid/` named after the entry's `appid` (`<id>.png`, `<id>p.png`, `<id>_hero.png`, `<id>_logo.png`; the extension follows the SteamGridDB URL, e.g. `.webp`/`.jpg`).

## Task 5
- [ ] Export with Steam running: Steam closes and reopens (normally within ~15 s; UWPHook waits up to 60 s), and the message says "Steam has been restarted." The app stays open.
- [ ] Export with Steam closed: the message says the games will show up the next time Steam starts, and Steam is not launched. The app stays open.
- [ ] Export with the SteamGridDB "animated" filter on: the grid images are saved with their original extension (e.g. `.webp`/`.png`), still animate, and Steam shows them. The app stays open.
- [ ] If Steam does not exit within 60 s (e.g. a modal dialog is open in Steam), export finishes with "Steam is still closing, so nothing was exported. Wait until it has closed, then export again. UWPHook will not start Steam again after this, so start it yourself." instead of hanging.
- [ ] Force an error during export (e.g. make `shortcuts.vdf` read-only): an error message box appears, the error is in `%AppData%\Briano\UWPHook\application.log`, and the window is re-enabled.

## Integration 4+5
- [ ] Re-export an existing game with the animated SGDB filter → the grid folder has only one file per art type (no `<id>p.png` left beside `<id>p.webp`, and likewise for `<id>`, `<id>_hero` and `<id>_logo`).
- [ ] An error raised while an error dialog is open does not stack dialogs (only one error box is shown; the later errors are in `application.log` with "An error dialog is already open").
- [ ] With a game whose own icon can't be read and whose SteamGridDB logo is a `.webp`/`.jpg`, export still uses the downloaded logo as the backup icon.

## Task 8b
- [ ] Forza Motorsport, Forza Horizon (4/5), Halo: The Master Chief Collection, Starfield and Gears of War: Ultimate Edition appear in the app list with readable names.
- [ ] A game installed on a non-system drive (e.g. `D:\XboxGames\...`) appears in the app list.
- [ ] The app list loads in under ~5 s after pressing Load.
- [ ] Exported games still get an icon (picked from the logo folder) and launch; Halo MCC still tracks `MCCWinStore-Win64-Shipping.exe`. Halo MCC shows one row per launchable app; check for an unexpected duplicate.
- [ ] A package with several Start-menu apps shows each app as its own row.

## Task 9b
- [ ] Settings → "Remove UWPHook shortcuts" removes only UWPHook entries; non-Steam games added by Steam itself or other tools (e.g. Heroic, Playnite, added by hand) remain, and a backup `<userid>_<timestamp>_shortcuts.vdf` appears in `%AppData%\Briano\UWPHook\backups`.
- [ ] With Steam running, "Remove UWPHook shortcuts" → Steam restarts and the entries stay gone (also after closing and reopening Steam). With Steam not running, the message says the changes apply the next time Steam starts; with nothing to remove, it says nothing was changed and Steam is not restarted.
- [ ] A non-Steam game added by another tool keeps its Steam collections and its "sort as" name after a UWPHook export.
- [ ] Steam starts normally and lists every shortcut after an export written by the new `shortcuts.vdf` writer (no lost or duplicated entries; a game exported with 2.14 keeps its art and playtime).
- [ ] A Steam user with no `config\shortcuts.vdf` yet gets one after an export, and the game shows up in Steam.
- [ ] With one user's `shortcuts.vdf` replaced by garbage bytes, export shows an error for that user only, leaves that file byte-for-byte unchanged, and still exports to the other users.
- [ ] No `shortcuts.vdf.tmp` is left in `userdata\<id>\config\` after an export, and `%TEMP%\UWPHook\tmp_grid` is gone afterwards, also after an export that failed.
- [ ] With an artwork file in `userdata\<id>\config\grid\` locked (e.g. opened in an image editor that holds a lock), export still copies the other images and other users' art; the failure is in `application.log`.

## Task 7
- [ ] Velopack `UWPHook-win-x64-Setup.exe` installs per-user without a UAC prompt into `%LocalAppData%\UWPHook` (`UWPHook.exe` stub + `Update.exe` in the root, the app in `current\`), adds Start menu/desktop shortcuts, and opens UWPHook.
- [ ] Exported shortcut from the installed app: `Exe` in `shortcuts.vdf` is `"%LocalAppData%\UWPHook\current\UWPHook.exe"` (expanded; **not** the root stub `%LocalAppData%\UWPHook\UWPHook.exe`) and `StartDir` is `"%LocalAppData%\UWPHook\current"`.
- [ ] Launching that shortcut from Steam: the game starts, Steam shows "Playing/Running" for the whole session (not only for a moment: the shortcut targets `current\UWPHook.exe` directly, so no launcher stub sits in between), and Steam's "Stop" behaves as with 2.14.
- [ ] Simulated update: install a 2.15.0 pack, then a 2.15.1 test pack (`vpk pack ... --packVersion 2.15.1` from the same channel, applied via `Update.exe apply` or by running the 2.15.1 Setup.exe over it); the Steam shortcuts exported from 2.15.0 still launch, no duplicate entries after re-export.
- [ ] Uninstall from Windows Settings → Apps: `%LocalAppData%\UWPHook` is removed, `%AppData%\Briano\UWPHook` (backups, icons, `application.log`) is left intact.
- [ ] The win-x64 `Portable.zip`, extracted to a folder with spaces (e.g. `D:\My Games\UWPHook`), runs; exported shortcuts point at the path of the exe that is actually running (`...\current\UWPHook.exe` for Velopack's zip, or the exe's own path for a plain unzipped build) and launch.
- [ ] The win-arm64 Setup.exe installs and runs on an ARM64 PC (or note it untested).
- [ ] Acceptance (brief Step 3) on Windows 11 25H2 and Windows 10 22H2: list apps, export, launch from Steam, re-export (no duplicates), stream mode.

## Task 10a
- [ ] With a valid SteamGridDB key, a game whose name contains `&` or `:` (e.g. "Halo: The Master Chief Collection", "Ori & the Will of the Wisps" style names) gets grid, wide grid, hero and logo artwork after export.
- [ ] With an invalid SteamGridDB key, export shows "Your SteamGridDB API key was rejected; artwork was skipped. Check it in Settings." once (not once per game), the shortcut is still added to Steam, and the key is still in Settings afterwards; `application.log` has the warning.
- [ ] Changing the SteamGridDB key in Settings and exporting again (without restarting UWPHook) uses the new key.
- [ ] Style "no_logo" in Settings returns no-logo grids (the stored value had a trailing space that is now trimmed).

## Task 11
- [ ] Halo MCC (its launcher exits): Steam shows "Playing" until the game itself closes, then UWPHook exits within about 10 s plus the check interval. With Settings → log level DEBUG, `application.log` has `Session for ... finished: Ended` (at the default ERROR level no session line is written).
- [ ] At the default log level (ERROR), a shortcut for a game that starts no process (e.g. uninstall the game but keep the shortcut, if activation still succeeds) logs the error `No process of <family> was seen after launching <AUMID>`, and UWPHook exits after about 10 s.
- [ ] Forza Horizon 5 is tracked for the whole session (Steam playtime matches how long you played).
- [ ] A game launched from Steam opens no UWPHook window with stream mode off (nothing in the taskbar or Alt+Tab, `UWPHook.exe` still in Task Manager while the game runs).
- [ ] Steam overlay (Shift+Tab) works in a game launched through UWPHook, with stream mode on and off.
- [ ] Steam Remote Play / in-home streaming of a UWPHook game, with stream mode on (the splash shows for about 10 s, then the game streams) and off.
- [ ] On an NVIDIA Advanced Optimus laptop: launching a game through UWPHook no longer blocks the switch to dGPU-only mode (#165), with stream mode on and off.
- [ ] Steam launch options (e.g. `-windowed` added after the executable in the shortcut's launch options) reach the game.
- [x] ~~With Settings → language override on: the game starts in the target language, and the Windows UI language is back to what it was after the game exits; with the override script failing (e.g. an invalid language), UWPHook still exits when the game ends.~~ *(superseded: language override removed in Task 13, R37)*
- [ ] A shortcut for a game that is no longer installed shows an error box and UWPHook exits; a shortcut whose launch options have no AUMID shows "Invalid launch arguments" and exits.
- [ ] Opening UWPHook normally (no arguments, Start menu) shows the games list; closing it exits the process.

## Task 10b
- [ ] Upgrade from 2.14.x: with 2.14 settings changed from the defaults (stream mode on, seconds = 3, a SteamGridDB key, style "No Logo", log level DEBUG, custom tags), install 2.15 and open Settings: every value is kept. `%AppData%\Briano\UWPHook\settings.json` now exists, and the first-run SteamGridDB offer does not appear again.
- [ ] Fresh install (no 2.14 user.config under `%LocalAppData%\Briano`): Settings shows the defaults (5 seconds, tags `READY TO PLAY,XBOX`, log level ERROR); the SteamGridDB offer appears once.
- [ ] Enter a SteamGridDB key on the first-run offer, restart UWPHook: the key is still there (it is no longer wiped on first run).
- [ ] `settings.json` does not contain the SteamGridDB key in plain text (only `steamGridDbApiKeyProtected`); export still downloads artwork with that key.
- [ ] Hand-corrupt `settings.json` (e.g. delete the closing `}`), start UWPHook: it starts with the defaults, the old file is now `settings.json.bad`, and `application.log` has the error. A second corruption keeps `settings.json.bad` and adds a timestamped `settings.json.bad-…`.
- [ ] Copy `settings.json` to another Windows user (or PC) and start UWPHook there: the other settings are kept and the SteamGridDB key is empty, no crash.
- [ ] Change settings, update UWPHook to a newer build (Velopack update): settings are kept.

## Fix wave 1
- [ ] I2: two apps with the same display name (e.g. two editions of a game, or rename one row to match another) exported together get two different `appid`s in `shortcuts.vdf`, and each shows its own SteamGridDB art in Steam.
- [ ] I3: with 2+ Steam accounts in `userdata`, after an export every account's UWPHook entry has the same `icon` path, `%AppData%\Briano\UWPHook\icons\<AUMID with invalid characters as _>.<ext>`, and the icons folder has one file per app (no file names with the AUMID repeated).
- [ ] I4: export with no app ticked says "Select at least one app to export." and Steam is not touched; with Steam uninstalled (or both registry keys removed) it says Steam was not found; with one account's `shortcuts.vdf` replaced by garbage and another account fine, it says "exported to 1 of 2 Steam accounts; 1 failed"; with every account's file broken, it shows an error and no success message.
- [ ] I5: Steam moved to `D:\Steam` (HKCU `SteamPath` points there) while HKLM `InstallPath` still names `C:\Program Files (x86)\Steam`: export writes to `D:\Steam\userdata\<id>\config\shortcuts.vdf`, and the restarted Steam is the `D:\Steam\steam.exe` one.
- [ ] M5: after more than 20 exports, `%AppData%\Briano\UWPHook\backups` holds the newest 20 backups per Steam account; other accounts' backups are untouched.
- [ ] M6: a `userdata\0` folder (or a non-numeric folder) gets no `config\shortcuts.vdf` or grid art after an export.
- [ ] M7: export with Steam running: Steam closes first, then the shortcuts are written, then Steam starts and shows the games (check `application.log` at DEBUG: "Requesting Steam shutdown" before "Shortcuts written", "Starting Steam" after). Same for Settings → "Remove UWPHook shortcuts": the removed entries stay gone after Steam restarts.
- [ ] M7: with a modal dialog open in Steam (so it doesn't exit within 60 s), export says "Steam is still closing, so nothing was exported. Wait until it has closed, then export again. UWPHook will not start Steam again after this, so start it yourself." and `shortcuts.vdf` is unchanged (no new backup).
- [x] ~~M10: with the language option on and an invalid target language (edit `targetLanguage` in `settings.json`), the game still launches and `application.log` has "Setting the Windows UI language to ... failed; launching anyway".~~ *(superseded: language override removed in Task 13, R37)*
- [ ] M12: a failure while loading the app list shows the error box in front of the UWPHook window.
- [ ] M2: a game whose executable path has a space (e.g. a GDK game with `Some Game.exe`, or edit a shortcut's launch options to `<AUMID> "Bin\Some Game.exe"`) gets `LaunchOptions` `<AUMID> "…\Some Game.exe"` in `shortcuts.vdf`. Steam passes it to UWPHook as one argument: the game launches, Steam shows "Playing" for the whole session, and at DEBUG `application.log` names the full executable, not `Some`.
- [ ] R33: with one account's `userdata\<id>\config\grid` made read-only (or a file there locked), export still says the apps were exported, adds "Artwork could not be copied for Steam account(s) <id>", and the other accounts get their art.
- [ ] R33: when steam.exe can't be found while Steam runs (rename `HKCU\Software\Valve\Steam\SteamExe` and start Steam from a copied folder not named in the registry), export says UWPHook "could not find steam.exe to close and restart it", not "Steam is still closing", and Steam is left running.

## Task 10c-1
Export and "Remove UWPHook shortcuts" now run through the Core `ShortcutExporter` / `ShortcutRemover`; re-run the earlier export cases.
- [ ] I4: export with nothing selected, with Steam not installed (or its registry keys removed), and with Steam never signed in: each shows its own message and nothing is written.
- [ ] I5: Steam moved to `D:\` with a stale HKLM key: the shortcuts go to `D:\Steam\userdata`, and the Steam that is restarted is `D:\Steam\steam.exe`.
- [ ] Fix-wave minor 4: a stale `HKLM\SOFTWARE\WOW6432Node\Valve\Steam\InstallPath` with a valid 64-bit `HKLM\SOFTWARE\Valve\Steam\InstallPath` (and no HKCU `SteamPath`): export finds the valid one.
- [ ] M7: export while Steam runs: Steam closes, the shortcuts are written, Steam starts again and shows them. Same for "Remove UWPHook shortcuts".
- [ ] R33: make one account's `config\grid` folder read-only: the export still succeeds for every account, and the message names that account for the artwork.
- [ ] A Steam account whose `loginusers.vdf` entry is missing (delete its block from `Steam\config\loginusers.vdf`, keep `userdata\<id>`): it is still exported to. A corrupt `loginusers.vdf` gives a warning in `application.log` and the export still reaches every account.
- [ ] M3: Tags `" A , ,B"` gives the Steam collections `A` and `B` only; an empty Tags box adds no collection.
- [ ] An invalid SteamGridDB key during export shows the "key was rejected" box once, then the export continues.
- [ ] Settings: delete `settings.json` and put a `settings.json.bad` next to it: the next start uses the defaults (2.14's settings are not imported again) and writes a new `settings.json`.

## Task 10c-2
The windows now run on ViewModels (`UWPHook.Presentation`) built once by the composition root (`Infrastructure/AppServices`); re-check that each behaves as before.
- [ ] Games list: it loads on start ("Loading your installed apps", list locked, progress bar), then shows "Installed Apps" sorted by name; the reload button loads it again.
- [ ] "Show system apps" is ticked by default (the list matches 2.14's, system apps included); unticking it reloads without them, ticking it brings them back.
- [ ] The filter box narrows the list by name or AUMID as you type, ignoring case; clearing it shows every app. "Select all" / "Select none" tick or clear only the apps the filter shows.
- [ ] Columns are as before (Selected, Name, Executable, Aumid, Icon, IconPath); Executable and Aumid are read-only, and a renamed Name is the name exported.
- [ ] Export: the list is locked while it runs, the same messages as 10c-1 show with the same icons (information / warning / error), and a failure shows "Exporting your games failed." with the details.
- [ ] Each exported app still gets its widest square logo as its Steam icon (now picked after the pre-checks), or the SteamGridDB logo when it has none.
- [ ] An invalid SteamGridDB key during export shows the "key was rejected" box once (now through the dialog service, from the export's worker thread), then the export continues.
- [ ] First start (delete `offeredSteamGridDb` from `settings.json`): the SteamGridDB question shows once; Yes opens Settings, No doesn't; it is not asked again.
- [ ] Settings shows every stored value (seconds, streaming, tags, log level, key, artwork choices); the seconds combo offers 1 to 9 seconds (no "0 seconds"). Save stores them, applies the log level, and closes.
- [ ] Settings save failure: make `settings.json` read-only (or lock it), change a value and Save: "Saving your settings failed." shows, the window stays open with your edits.
- [ ] R37: Settings no longer shows the language options (the "Set system language" toggle and the language combo); saving drops `changeLanguage`/`targetLanguage` from `settings.json` (Task 13, R37).
- [ ] Steam user picker (#76) with one Steam account: it is listed as `PersonaName (AccountName)` (or its id without names), ticked, marked "(most recent)".
- [ ] Steam user picker with 2+ accounts: all ticked by default; untick one and Save: export and "Remove UWPHook shortcuts" only touch the ticked accounts (check each `userdata\<id>\config\shortcuts.vdf`). Untick all and Save: "Choose at least one Steam account..." shows and nothing is saved.
- [ ] An account removed after being chosen (choose only account B, then delete `userdata\<B>`): Settings ticks the remaining accounts, and export writes to all of them.
- [ ] "Remove UWPHook shortcuts": the warning question shows first; No does nothing; Yes removes and shows the same messages as before; the button is disabled while it runs.
- [ ] The links (Check for updates, Reddit, GitHub, PayPal, Twitter) and the key button (info box, then the SteamGridDB API page) open as before.
- [ ] A launch from Steam (stream mode, poll seconds) behaves as before; the settings are read through the composition root.
- [ ] Fix round 1 (R38): sort the games list by a column, then type in the filter box: the sort and the columns stay, and a Name edit in progress is not lost; clearing the filter brings back every app, still sorted.
- [ ] The list is ordered by the friendly name from `KnownApps.json` (2.14 sorted by the package's name, then renamed), e.g. a known app sits under its friendly name.
- [ ] Choose only account B in Settings, then delete `userdata\<B>`: export and Remove use every account, and `application.log` has the Warning "The Steam accounts chosen in Settings (…) no longer exist; using every account".
- [ ] The Remove question says it removes UWPHook's shortcuts from the Steam accounts saved in Settings.

## Task 12
- [ ] On Windows 10 and Windows 11, the games list and Settings follow the system's light/dark setting (switch it while UWPHook is open, and on a fresh start); Windows 11 may show Mica, Windows 10 shows a plain background.
- [ ] `%AppData%\Briano\UWPHook\application.log` has no missing-resource (`ResourceReferenceKeyNotFoundException`/`XamlParseException`) entry after opening the games list, Settings and a stream-mode launch.
- [ ] Every icon shows, with no empty boxes, on Windows 10 (Segoe MDL2 Assets) and Windows 11 (Segoe Fluent Icons): Settings (gear), Load (refresh), the SteamGridDB key button, and the GitHub/PayPal/@brianostorm buttons in Settings and the splash.
- [ ] In both themes, the Settings Steam account picker, the "Enable streaming mode" check box, the "Show system apps" check box and the games DataGrid (headers, rows, the selection checkboxes) are readable.
- [ ] The stream splash (stream mode on, launch a game from Steam) is dark even with Windows in light mode, and still renders in software (NVIDIA Advanced Optimus switches to the dGPU, #165).
- [ ] The header band behind the white UWPHook logo uses the Windows accent colour and the white welcome text is readable on it.

## Task 13
- [ ] No `powershell.exe` (or `pwsh.exe`) is spawned while listing apps, exporting, or launching a game from Steam (Process Monitor filter "Process Name is powershell.exe", or Task Manager).
- [ ] A moddable GDK game installed under `XboxGames` (e.g. `C:\XboxGames\<Game>\Content`) is listed with that folder as its install location, launches from Steam, and Steam shows "Playing" until the game exits.
- [ ] On Windows 10 1809 (build 17763), if available: the app list loads and a game launches and is tracked until it exits.
- [ ] An old `settings.json` that still has `"changeLanguage": true, "targetLanguage": "ja-JP"` loads with every other setting kept, the Windows display language is not touched at launch, and the next Save drops the two keys.

## Final fix wave
- [ ] C1, on a PC with 2.14 installed by its NSIS installer (into `%AppData%\Briano\UWPHook`): install 2.15 Setup, then check Windows Settings > Apps: how many "UWPHook" entries are listed, and which uninstaller each runs (Velopack's `Update.exe --uninstall` or 2.14's `uninstall.exe`). Check whether Velopack's uninstall entry and 2.14's `HKCU\Software\Microsoft\Windows\CurrentVersion\Uninstall\UWPHook` key collide (same key, one overwriting the other). If they do, the README's "do not uninstall 2.14 from Settings > Apps" warning is essential; note the result here.
- [ ] C1: with 2.14 and 2.15 installed, the 2.14 Start menu folder "UWPHook" still opens 2.14, the desktop `UWPHook` shortcut opens whichever version wrote it last (note which), and 2.15's own Start menu entry opens 2.15.
- [ ] C1: after following the README upgrade steps (open 2.15, export again, delete only 2.14's listed program files and Start menu folder), `%AppData%\Briano\UWPHook\{settings.json,backups,icons,application.log}` still exist, every Steam shortcut launches through `%LocalAppData%\UWPHook\current\UWPHook.exe`, and the icons show in Steam.
- [ ] I1: press Export with Steam running, answer Yes to the close-Steam question, then at once try to close the games window (title-bar X and Alt+F4): it stays open and says UWPHook is still exporting; after the export, Steam is running again and the window closes normally.
- [ ] I1: in Settings, press "Remove UWPHook shortcuts" with Steam running, answer Yes to both questions, then at once try to close Settings (X, Esc if bound, Save): it stays open and says UWPHook is still removing; afterwards Steam is running again.
- [ ] m3: with a SteamGridDB key set and a leftover `%TEMP%\UWPHook\tmp_grid\<AUMID>` folder held open by another program (e.g. a Command Prompt `cd`'d into it, so it can't be deleted), export still writes the shortcuts and says "Artwork could not be downloaded for <app>"; the error is in `application.log`.
- [ ] m4: a 2.14 backup (`<userid>_<yyyyMMddHHmmss>_shortcuts.vdf`, 14 digits) in `%AppData%\Briano\UWPHook\backups` is still there after more than 20 exports with 2.15.
- [ ] Settings: clicking the text "Enable streaming mode" ticks and unticks the box.
- [ ] Velopack: run a 2.15.1 Setup while a game launched through UWPHook is running. Note whether Setup kills `current\UWPHook.exe`; if it does, Steam's "Playing" ends early (a known limitation to document for Phase 3 updates).
- [ ] Session end for a non-game UWP app with background tasks (e.g. Mail/Calendar-style apps): launch it from Steam, close it; UWPHook exits within the grace period plus one poll (package-family tracking also counts `backgroundTaskHost.exe` processes of that package).
- [ ] While "Remove UWPHook shortcuts" is running from Settings, try to close the main window from the taskbar: the app must stay open until the removal ends and Steam is started again. If it closes, report it (the main window does not yet check a removal in progress).
- [ ] After deleting 2.14's `UWPHook` Start menu folder (upgrade step 3), 2.15's own Start menu entry is still there and starts 2.15.

## Missing apps
- [ ] Fallout 76 (if installed): it is listed as "Fallout 76" with Executable `Project76_GamePass.exe`; export it and launch it from Steam: it starts and Steam shows "Playing" until the game exits.
- [ ] One other GDK game (installed to an `XboxGames` folder, manifest executable `GameLaunchHelper.exe`) and one plain UWP app (e.g. Calculator, with "Show system apps" ticked) are listed with their executables, and launch from Steam.
- [ ] In Settings set the Log level to DEBUG, press Load: `application.log` has one "App list loaded: N packages read, N apps listed (N without an executable), N packages skipped" line per load, and any "Package <family name>: ..." warnings name the package and the reason. Note the counts here.
- [ ] If any app is listed with an empty Executable cell: the cell is blank (not "null"); export it, and in Steam's shortcut properties its launch options are `<AUMID> ""` (the AUMID, a space and two double quotes); launching it from Steam starts the app and Steam's "Playing" ends within the grace period after it closes.
- [ ] For that same shortcut, append ` -test` in Steam's launch options (`<AUMID> "" -test`), set the log level to TRACE and launch it: `application.log`'s "Launch request =>" line shows `Executable = ` (empty) and `ExtraArguments = -test`, and the app still starts. (This checks that Steam passes the launch options verbatim and Windows turns `""` into one empty argument.)
- [ ] Close Steam, edit that shortcut's launch options to the AUMID alone (no `""`), and export the app again: the same shortcut is updated to `<AUMID> ""`, and no duplicate appears.

## Manual-test fix wave 1
- [ ] Finding 1: Settings at 100%, 125% and 150% display scaling, at its default size and dragged to its minimum width: no label, combo box value ("5 seconds", "Any"), tags or API key box is cut off or overlaps another control; with two or more Steam accounts each is on its own row as `PersonaName (AccountName)`, all ticked by default; the SteamGridDB key shows as dots until the eye button is pressed, and a key typed in either state is the one saved.
- [ ] Finding 2: in the Windows dark theme, toggle "Show system apps" and press Load: the games list dims under the dark background with a progress bar and never flashes white; the same during an export.
- [ ] Finding 3: with Steam running, export: UWPHook asks "Steam must be closed to write your shortcuts. Close Steam now? It is started again afterwards." No: Steam keeps running and UWPHook says "Nothing was exported. Close Steam and export again."; Yes: Steam closes, the shortcuts are written and Steam starts again. The same for Settings > "Remove UWPHook shortcuts" ("Nothing was removed. Close Steam and try again."). With Steam closed, neither asks.
- [ ] Finding 4: launch a game from Steam (Big Picture and desktop): the game comes to the foreground in front of Steam; with stream mode on, the splash is gone before the game appears and never covers it. With the log level at DEBUG, `application.log` has no "CoAllowSetForegroundWindow failed" line (if it has, note the HRESULT).
- [ ] While an export runs, Tab into the games list and try Space (tick) and F2 (rename): nothing changes until the export ends.

## Tags / collections
- [ ] Settings shows "Add games to Steam collections" with the old tags value (`READY TO PLAY,XBOX` by default); the label and its box are not cut off at 100/125/150% scaling and at the window's minimum width.
- [ ] With Steam running and signed in, export one game with `READY TO PLAY,XBOX` (answer Yes to closing Steam). After Steam restarts, the library's Collections view has "READY TO PLAY" and "XBOX" (created if they did not exist) and the game is in both. `%AppData%\Briano\UWPHook\backups` has a new `{userid}_{timestamp}_collections.json` that is the file from before the export.
- [ ] If a collection named "XBOX" already existed with other games (Steam games and other tools' shortcuts), they are all still in it, the collection was not duplicated, and its name is unchanged. Also try a setting in another case (`xbox`): the game goes into the existing "XBOX", no second collection appears.
- [ ] The collections survive a second Steam restart, and a sign-out/sign-in of Steam (this is the check that Steam's cloud sync does not undo them). Note whether a collection UWPHook created also shows up on a second PC signed in to the same account (not expected for the games themselves: shortcuts are per PC).
- [ ] In Steam, remove the game from "XBOX" by hand, then export it again: note what happens (expected: it is added back, because "XBOX" is a normal collection; in a *dynamic* collection a game removed by hand stays removed).
- [ ] Delete the "READY TO PLAY" collection in Steam, restart Steam, export again: the collection is back with the game in it.
- [ ] Export again without changing anything: no new `_collections.json` backup is made and the collections are as before.
- [ ] Change the setting to `GAME PASS` and export: the game is added to "GAME PASS" and stays in "READY TO PLAY" and "XBOX" (UWPHook never removes). Empty the setting and export: no collection changes, and no "could not be added to your Steam collections" note.
- [ ] Other collection data is intact after an export: Favorites, hidden games, dynamic collections and the library home shelves look as before.
- [ ] With two Steam accounts ticked, one of which never signed in on this client version (no `config\cloudstorage` folder): the export succeeds as plain information (no warning icon) that says "Steam account(s) <id> have no Steam collections yet, so the games were not added to collections there", and `application.log` names the missing file at Information level.
- [ ] Make `cloud-storage-namespace-1.json` unreadable (replace its content with `x` while Steam is closed, keeping a copy): the export succeeds as a warning that says the games "could not be added to your Steam collections for Steam account(s) <id>", and the file is still `x`. Put the copy back.
- [ ] Open `shortcuts.vdf` of a re-exported game that had extra tags from an older UWPHook or another tool: the old tags are still there, followed by the ones from Settings, without doubles.
- [ ] Note the Steam client build (Help > About Steam) these were checked on, and whether `config\cloudstorage\cloud-storage-namespace-1.modified.json` changed after the export and the Steam restart.
- [ ] Set the setting to `Favorites` and export: note what happens (expected: the game appears under Steam's own Favorites, because the name matches that built-in collection, and no second "Favorites" collection is created). The same with `Hidden` (expected: the game is hidden from the library; un-hide it afterwards via View > Hidden games). If either creates a duplicate collection instead, report it.
- [ ] Create a dynamic collection in Steam (for example "Installed locally", filtered on "Installed locally"), set the setting to its name and export: the game is in that collection, the collection is still dynamic with its filter, and its other games are unchanged. Then remove the game from it by hand in Steam and export again: it stays removed.
- [ ] After reviving a deleted collection (the "Delete READY TO PLAY" item above) restart Steam twice: the collection does not disappear again (a release gate, like the sign-out check: the revived entry keeps the version number of its deletion).
- [ ] Change the setting from `XBOX` to `xbox` and export: still one "XBOX" collection, and the shortcut's `tags` in `shortcuts.vdf` still has one `XBOX` entry.

## Icon column
- [ ] Open UWPHook: the list appears first, and each app's icon (32x32) appears in the Icon column, right after Selected, within a few seconds. Apps without an icon show an empty cell (no error, no "null"). Hovering an icon shows its logo folder. The columns are Selected, Icon, Name, Executable, Aumid; Name can still be edited (F2) and sorting by any text column works.
- [ ] The icons look right in both the Windows light and dark theme (no white box behind transparent icons), and are sharp (not blurry) at 100%, 125% and 150% display scaling. An app without a logo folder shows no tooltip (no empty box) over its Icon cell.
- [ ] With a large library ("Show system apps" ticked), the window stays responsive (scroll, type in the filter, tick apps) while the icons are still coming in; press Load again (or toggle "Show system apps") halfway: the new list fills in its icons, and no error appears. Once the icons are in, typing in the filter and scrolling feel as quick as with no icons (note roughly how long the icons took for how many apps).
- [ ] Export a game, then in Steam look at the shortcut's icon: it is the same image the list showed for it (and `%AppData%\Briano\UWPHook\icons` has its copy). With the log level at DEBUG, any logo file UWPHook could not read is logged as "Skipped ... it is not a readable image", and the app is still listed.
- [ ] While the list is shown, uninstall or update a game whose icon is showing (or rename one of its logo files): UWPHook does not hold the file open (the update/uninstall is not blocked by UWPHook).

## Final fix wave 2
- [ ] Rename `steam.exe` while UWPHook has Steam closed for an export (or point `HKCU\Software\Valve\Steam\SteamPath` at a copy and delete the copy's `steam.exe` right after answering Yes): the export message ends with "Steam could not be started again; start it yourself." (a warning), not "Steam has been restarted.", and the shortcuts are written.
- [ ] Deny your own user read access to one game's logo folder (Properties > Security), then export that game and another: both are exported, the first without an icon, and `application.log` (WARN or lower) has "Could not pick the icon of ...".
- [ ] In Steam, append `-mod "C:\My Mods"` to an exported shortcut's launch options and start it: the game receives `-mod` and `C:\My Mods` as two arguments, not three (check with a tool such as Process Explorer > Command line, or a game that shows its arguments).

## Before tagging 2.15.0
None of these has ever been run. They are the gate for the tag.
- [ ] `Setup.exe` from a `workflow_dispatch` run of `release.yml`: installs without UAC, exports with `Exe` = `...\current\UWPHook.exe`, and Steam shows "Playing" for the whole session (see "Task 7").
- [ ] `Portable.zip` from the same run, extracted over a 2.14 portable folder: launch an old shortcut that was not exported again and note whether Steam keeps "Playing". (The README and CHANGELOG tell every upgrader to export again because the root `UWPHook.exe` is Velopack's stub; if Steam does keep "Playing", that wording can be relaxed.) Then export again and check the shortcut now points at `current\UWPHook.exe` and Steam shows "Playing".
- [ ] Upgrade on a PC with the NSIS 2.14 installed: settings are imported, exporting again updates the shortcuts in place with no duplicates, and `%AppData%\Briano\UWPHook\{settings.json,backups,icons}` are intact.
- [ ] Collections after a second Steam restart and after signing out of Steam and in again (see "Tags / collections", items 4 and 13): the games are still in the collections. This is the only check that Steam's cloud sync keeps what UWPHook wrote.
- [ ] An app listed without an executable: its launch options are `<AUMID> ""`, it launches, and ` -test` appended in Steam arrives as an extra argument (see "Missing apps", items 4 and 5).
- [ ] Answer Yes with a Steam that is slow to exit (start a download or a cloud sync first). Within 60 s: Steam closes, the shortcuts are written, Steam starts again. Past 60 s: the message says Steam is still closing, that nothing was exported, and to start Steam yourself; Steam is then closed (or closes shortly after), and exporting again works without a question.
- [ ] Set the release date in `CHANGELOG.md` (`## [2.15.0] - Unreleased`) when tagging.
