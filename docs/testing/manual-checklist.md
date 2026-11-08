# Manual Windows checklist

Checks that need a real Windows machine (they cannot run in CI). Tick them off on a Windows box before a release.

## Task 1
- [ ] The published `UWPHook.exe` (CI artifact `UWPHook-win-x64`) opens, and Settings shows the version `2.15.0.0`.
- [ ] The publish folder contains a single-file `UWPHook.exe` plus native runtime DLLs in the same folder (no `.pdb` files), and running it does not extract anything to `%TEMP%` (no new `.net\UWPHook` folder under `%TEMP%`).
- [ ] `%AppData%\Briano\UWPHook\application.log` is still written after launching the app.

## Task 2
- [ ] Re-exporting a game that UWPHook added with 2.14 gives the same `appid` in `shortcuts.vdf`, and its SteamGridDB grid/hero/logo images still show in Steam (the id switched from Crc32.NET to `SteamShortcutId`).

## Task 6
- [ ] With PowerShell 7 installed and set as the default shell, the app list loads (discovery runs through `powershell.exe` 5.1 out-of-process).
- [ ] Forza Motorsport, or any app whose manifest name is an `ms-resource:` reference, appears in the list (PR #169).
- [ ] Two different games whose names share a prefix (e.g. "Hollow Knight" and "Hollow Knight: Silksong") both appear (PR #182).
- [ ] Changing settings and pressing Save doesn't crash, including with no language selected; the seconds value round-trips after reopening Settings.
- [ ] The Settings window no longer shows the "Change host resolution" toggle/combo.
- [ ] With "Set system language when launching an app" on, launching a game switches the UI language and it is restored after the game closes.
- [ ] No console window flashes while the app list loads.
