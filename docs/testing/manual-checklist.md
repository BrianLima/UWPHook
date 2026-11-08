# Manual Windows checklist

Checks that need a real Windows machine (they cannot run in CI). Tick them off on a Windows box before a release.

## Task 1
- [ ] The published `UWPHook.exe` (CI artifact `UWPHook-win-x64`) opens, and Settings shows the version `2.15.0.0`.
- [ ] The publish folder contains a single-file `UWPHook.exe` plus native runtime DLLs in the same folder (no `.pdb` files), and running it does not extract anything to `%TEMP%` (no new `.net\UWPHook` folder under `%TEMP%`).
- [ ] `%AppData%\Briano\UWPHook\application.log` is still written after launching the app.

## Task 2
- [ ] Re-exporting a game that UWPHook added with 2.14 gives the same `appid` in `shortcuts.vdf`, and its SteamGridDB grid/hero/logo images still show in Steam (the id switched from Crc32.NET to `SteamShortcutId`).

## Task 4
- [ ] Export a game with UWPHook 2.14.3, upgrade to 2.15, and re-export it: Steam shows exactly one entry for the game (same `appid` in `shortcuts.vdf`), its SteamGridDB grid/hero/logo art is kept, and it launches when UWPHook is installed in a path with spaces (e.g. `C:\Program Files (x86)\...`).
- [ ] In `shortcuts.vdf` after the export, the UWPHook entry has `Exe` and `StartDir` wrapped in double quotes, and shortcuts added by other tools (or by hand) are unchanged.
- [ ] With a SteamGridDB key set, exporting a new game puts its images in `userdata/<id>/config/grid/` named after the entry's `appid` (`<id>.png`, `<id>p.png`, `<id>_hero.png`, `<id>_logo.png`).
