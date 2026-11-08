# Manual Windows checklist

Checks that need a real Windows machine (they cannot run in CI). Tick them off on a Windows box before a release.

## Task 1
- [ ] The published `UWPHook.exe` (CI artifact `UWPHook-win-x64`) opens, and Settings shows the version `2.15.0.0`.
- [ ] The publish folder contains a single-file `UWPHook.exe` plus native runtime DLLs in the same folder (no `.pdb` files), and running it does not extract anything to `%TEMP%` (no new `.net\UWPHook` folder under `%TEMP%`).
- [ ] `%AppData%\Briano\UWPHook\application.log` is still written after launching the app.

## Task 2
- [ ] Re-exporting a game that UWPHook added with 2.14 gives the same `appid` in `shortcuts.vdf`, and its SteamGridDB grid/hero/logo images still show in Steam (the id switched from Crc32.NET to `SteamShortcutId`).

## Task 3
- [ ] A Steam shortcut whose `Exe` is UWPHook's path **without quotes** (in a folder with spaces, e.g. `C:\Program Files (x86)\Briano\UWPHook\UWPHook.exe`) launches the game instead of failing with "Value does not fall within the expected range".
- [ ] A shortcut whose launch options use forward slashes in the executable (e.g. Halo MCC `MCC/Binaries/...`) launches, and UWPHook keeps running until the game exits.
- [ ] Launching UWPHook with a junk argument (e.g. `UWPHook.exe foo`) shows "Invalid launch arguments", and `application.log` lists the arguments.
- [ ] Launching a shortcut for an uninstalled app shows the "Error while trying to launch your app." message, and `application.log` has a line with `HRESULT 0x........`.
- [ ] AUMID-only shortcut (no executable arg) launches, and UWPHook exits when the game's original process exits, without latching onto another process.

## Task 6
- [ ] With PowerShell 7 installed and set as the default shell, the app list loads (since Task 8b discovery uses the WinRT `PackageManager`, not PowerShell).
- [ ] Forza Motorsport, or any app whose manifest name is an `ms-resource:` reference, appears in the list (PR #169).
- [ ] Two different games whose names share a prefix (e.g. "Hollow Knight" and "Hollow Knight: Silksong") both appear (PR #182).
- [ ] Changing settings and pressing Save doesn't crash, including with no language selected; the seconds value round-trips after reopening Settings.
- [ ] The Settings window no longer shows the "Change host resolution" toggle/combo.
- [ ] With "Set system language when launching an app" on, launching a game switches the UI language and it is restored after the game closes.
- [ ] No console window flashes while the app list loads (the `PackageManager` catalog starts no process).

## Task 4
- [ ] Export a game with UWPHook 2.14.3, upgrade to 2.15, and re-export it: Steam shows exactly one entry for the game (same `appid` in `shortcuts.vdf`), its SteamGridDB grid/hero/logo art is kept, and it launches when UWPHook is installed in a path with spaces (e.g. `C:\Program Files (x86)\...`).
- [ ] In `shortcuts.vdf` after the export, the UWPHook entry has `Exe` and `StartDir` wrapped in double quotes, and shortcuts added by other tools (or by hand) are unchanged.
- [ ] With a SteamGridDB key set, exporting a new game puts its images in `userdata/<id>/config/grid/` named after the entry's `appid` (`<id>.png`, `<id>p.png`, `<id>_hero.png`, `<id>_logo.png`; the extension follows the SteamGridDB URL, e.g. `.webp`/`.jpg`).

## Task 5
- [ ] Export with Steam running: Steam closes and reopens within ~15 s, and the message says "Steam has been restarted." The app stays open.
- [ ] Export with Steam closed: the message says the games will show up the next time Steam starts, and Steam is not launched. The app stays open.
- [ ] Export with the SteamGridDB "animated" filter on: the grid images are saved with their original extension (e.g. `.webp`/`.png`), still animate, and Steam shows them. The app stays open.
- [ ] If Steam does not exit within 15 s (e.g. a modal dialog is open in Steam), export finishes with "Please restart Steam to see your games." instead of hanging.
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
- [ ] With Settings → language override on: the game starts in the target language, and the Windows UI language is back to what it was after the game exits; with the override script failing (e.g. an invalid language), UWPHook still exits when the game ends.
- [ ] A shortcut for a game that is no longer installed shows an error box and UWPHook exits; a shortcut whose launch options have no AUMID shows "Invalid launch arguments" and exits.
- [ ] Opening UWPHook normally (no arguments, Start menu) shows the games list; closing it exits the process.

## Task 10b
- [ ] Upgrade from 2.14.x: with 2.14 settings changed from the defaults (language override on with a target language, stream mode on, seconds = 3, a SteamGridDB key, style "No Logo", log level DEBUG, custom tags), install 2.15 and open Settings: every value is kept. `%AppData%\Briano\UWPHook\settings.json` now exists, and the first-run SteamGridDB offer does not appear again.
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
- [ ] M7: with a modal dialog open in Steam (so it doesn't exit within 15 s), export says "Steam did not close in time, so nothing was exported. Close Steam, then export again." and `shortcuts.vdf` is unchanged (no new backup).
- [ ] M10: with the language option on and an invalid target language (edit `targetLanguage` in `settings.json`), the game still launches and `application.log` has "Setting the Windows UI language to ... failed; launching anyway".
- [ ] M12: a failure while loading the app list shows the error box in front of the UWPHook window.
- [ ] M2: a game whose executable path has a space (e.g. a GDK game with `Some Game.exe`, or edit a shortcut's launch options to `<AUMID> "Bin\Some Game.exe"`) gets `LaunchOptions` `<AUMID> "…\Some Game.exe"` in `shortcuts.vdf`. Steam passes it to UWPHook as one argument: the game launches, Steam shows "Playing" for the whole session, and at DEBUG `application.log` names the full executable, not `Some`.
- [ ] R33: with one account's `userdata\<id>\config\grid` made read-only (or a file there locked), export still says the apps were exported, adds "Artwork could not be copied for Steam account(s) <id>", and the other accounts get their art.
- [ ] R33: when steam.exe can't be found while Steam runs (rename `HKCU\Software\Valve\Steam\SteamExe` and start Steam from a copied folder not named in the registry), export says UWPHook "could not find steam.exe to close and restart it", not "did not close in time", and Steam is left running.
