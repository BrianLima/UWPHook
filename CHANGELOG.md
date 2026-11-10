# Changelog

All notable changes to UWPHook are documented here.
The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and the project follows [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [2.15.0] - Unreleased

A stabilization release: the app list loads again, existing shortcuts launch again, and exporting no longer hangs or crashes.

### Upgrading from 2.14
- **Do not run the 2.14 uninstaller** (not from its Start menu entry, and not from Windows Settings > Apps), **and do not delete `%AppData%\Briano\UWPHook`.** The 2.14 installer put UWPHook into that folder, which is where 2.15 keeps your settings, `shortcuts.vdf` backups and the icons your Steam shortcuts use, and the 2.14 uninstaller deletes the whole `%AppData%\Briano` folder.
- Install 2.15, open it once (your settings are imported), then **export your games again**. Until you do, shortcuts made by 2.14 keep starting the old 2.14 `UWPHook.exe`.
- After that, you can remove 2.14 by deleting only its program files from `%AppData%\Briano\UWPHook` (`UWPHook.exe`, `UWPHook.exe.config`, `uninstall.exe`, `UWPHook website.url`, the `.dll` and `.xml` files and the `Resources` folder) and its `UWPHook` Start menu folder. Keep `settings.json`, the `application*.log` files, `backups` and `icons`. The [README](README.md#upgrading-from-214-or-older) has the details.

### Added
- Settings → "Remove UWPHook shortcuts" removes only the shortcuts UWPHook created, from the chosen Steam accounts, after backing up `shortcuts.vdf`. Shortcuts from Steam and other tools are kept.
- Settings lets you choose which Steam accounts export and remove use, listed by their Steam name (#76). All accounts are used by default.
- Search no longer resets the list's sort order, and "Select all/none" and a "Show system apps" option were added to the main window.
- A per-user installer (`UWPHook-win-x64-Setup.exe`), built with [Velopack](https://velopack.io). It installs to `%LocalAppData%\UWPHook` without asking for administrator rights. A portable zip is still available.
- Builds for ARM64 PCs (`win-arm64`) alongside x64.
- A global error handler. Unexpected errors are written to `application.log` and shown in a message box, and the app stays open instead of closing silently.

### Changed
- UWPHook now runs on .NET 10 as a self-contained single-file app. No separate .NET runtime install is needed.
- App discovery uses Windows' package manager directly instead of a bundled PowerShell 7 script, so it no longer depends on PowerShell or the Appx module. The app no longer changes your PowerShell execution policy, and it no longer runs PowerShell at all.
- Steam shortcuts of installed copies point at `%LocalAppData%\UWPHook\current\UWPHook.exe`, a path that stays the same across updates, so updating UWPHook doesn't break or duplicate your shortcuts.
- Artwork from SteamGridDB is saved exactly as downloaded (PNG, JPEG or WebP) instead of being re-encoded. Artwork is downloaded for at most 4 games at a time.
- When Steam is running, export (and "Remove UWPHook shortcuts") now closes Steam, writes the shortcuts, and starts it again, instead of writing first and restarting afterwards. If Steam doesn't close within 15 seconds, nothing is written and UWPHook asks you to close Steam.
- Only the newest 20 backups of `shortcuts.vdf` per Steam account are kept. Backups made by 2.14 are never deleted.
- The "Change resolution" option was removed. It did not work on desktop Windows (#124), and it will return in 3.0.
- The NSIS installer was replaced by the Velopack installer.
- The app uses Windows' built-in Fluent look and follows the system light/dark setting.
- Moddable Xbox games installed to an `XboxGames` folder are found, and their playtime tracked, from the folder they run from (Windows 10 1903 and later).
- Games are started with Windows' activation manager running out of process, as Microsoft recommends for an app started only to launch another.

### Removed
- The per-game language option. Windows only applies a display-language change after you sign out, so it never worked when launching a game. If a previous version left your Windows display language changed, set it back in Settings > Time & language > Language & region.

### Fixed
- **The app list is empty, or shows "Error trying to get installed apps"** (the Appx module could not load in the bundled PowerShell 7): #141 #149 #155 #164 #171 #178 #181 (also #132).
- **"Value does not fall within the expected range" when launching from Steam**, and games not starting when UWPHook is in a folder with spaces: #157 #160 #168.
  - Shortcuts now quote their `Exe` and `Start In` paths.
  - Shortcuts from older versions are recognised and updated in place when you export again from 2.15 (same app id, artwork and playtime). Until then they keep starting the old `UWPHook.exe`, so installer users must export again to get the fix. Portable users who replace the old `UWPHook.exe` in its folder get the launch fix without re-exporting.
- **Exporting no longer creates duplicate shortcuts or orphans your artwork and playtime.** Re-exporting updates UWPHook's existing entry in place, matched by the app's AUMID, and keeps its Steam app id. Exporting never changes or removes shortcuts created by other tools.
- **Export hangs or closes the app** (#166 #176, also #80 #143 #158).
  - Restarting Steam now gives up after 15 seconds instead of waiting forever. If it gives up, UWPHook asks you to restart Steam yourself.
  - Errors during export are reported instead of closing the app.
  - Closing UWPHook while it exports (or removes its shortcuts) is refused until it has finished, so Steam is never left closed.
  - Artwork that fails to download for one game no longer fails the whole export: the game is exported without it, and the message names it.
- **The settings window crashed on Save** (#162).
- **"An item with the same key has already been added" when reading `shortcuts.vdf`** (#139).
- **Forza Motorsport and other games with localized names were missing from the list** (#127). Thanks to @icavalheiro (PR #169).
- **Different games from the same publisher were hidden as duplicates** (for example Hollow Knight and Hollow Knight: Silksong). Thanks to @RXA7 (PR #182).
- A game launched without an executable name is no longer matched to an unrelated running process.
- Artwork downloaded in a different image format replaces the old file instead of leaving a stale copy next to it.
- **Settings were reset after every update, and the SteamGridDB key was wiped on first run.**
  - Settings now live in `%AppData%\Briano\UWPHook\settings.json`, imported once from 2.14.
  - The SteamGridDB key is encrypted for your Windows user.
  - A damaged settings file is set aside as `settings.json.bad`, and UWPHook starts with the defaults.
- An invalid SteamGridDB API key is no longer wiped: export says once that the key was rejected, skips artwork, and the key stays in Settings.
- Game names with `&`, `:` or other special characters find their SteamGridDB artwork.
- **Export no longer reports success when nothing was exported.** It says when no app is selected, when Steam or a Steam account was not found, and when writing failed for some or all accounts.
- **Export uses the Steam install the Steam client runs from**, so a Steam moved to another drive gets the shortcuts (the installer's registry path can be stale).
- Two apps with the same name (for example two editions of a game) get different Steam app ids, so they no longer share or overwrite each other's artwork.
- With several Steam accounts, every account gets the same icon file, instead of a copy with an ever longer name that could fail past the 4th account.
- Only real Steam account folders (numeric ids) get shortcuts; Steam's `0` folder and other folders in `userdata` are skipped.
- A game whose executable has a space in its name is tracked correctly.
- A non-Steam shortcut from another tool with an unusual app id no longer blocks the export for that Steam account.

[2.15.0]: https://github.com/BrianLima/UWPHook/compare/2.14.3...v2.15.0
