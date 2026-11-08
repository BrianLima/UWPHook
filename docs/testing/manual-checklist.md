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

## Task 8b
- [ ] Forza Motorsport, Forza Horizon (4/5), Halo: The Master Chief Collection, Starfield and Gears of War: Ultimate Edition appear in the app list with readable names.
- [ ] A game installed on a non-system drive (e.g. `D:\XboxGames\...`) appears in the app list.
- [ ] The app list loads in under ~5 s after pressing Load.
- [ ] Exported games still get an icon (picked from the logo folder) and launch; Halo MCC still tracks `MCCWinStore-Win64-Shipping.exe`.
- [ ] A package with several Start-menu apps shows each app as its own row.
