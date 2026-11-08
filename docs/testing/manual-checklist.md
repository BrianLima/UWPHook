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
- [ ] With PowerShell 7 installed and set as the default shell, the app list loads (discovery runs through `powershell.exe` 5.1 out-of-process).
- [ ] Forza Motorsport, or any app whose manifest name is an `ms-resource:` reference, appears in the list (PR #169).
- [ ] Two different games whose names share a prefix (e.g. "Hollow Knight" and "Hollow Knight: Silksong") both appear (PR #182).
- [ ] Changing settings and pressing Save doesn't crash, including with no language selected; the seconds value round-trips after reopening Settings.
- [ ] The Settings window no longer shows the "Change host resolution" toggle/combo.
- [ ] With "Set system language when launching an app" on, launching a game switches the UI language and it is restored after the game closes.
- [ ] No console window flashes while the app list loads.

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
