# Changelog

All notable changes to UWPHook are documented here.
The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and the project follows [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [2.15.0] - Unreleased

A stabilization release: the app list loads again, existing shortcuts launch again, and exporting no longer hangs or crashes.

### Added
- A per-user installer (`UWPHook-win-x64-Setup.exe`), built with [Velopack](https://velopack.io). It installs to `%LocalAppData%\UWPHook` without asking for administrator rights. A portable zip is still available.
- Builds for ARM64 PCs (`win-arm64`) alongside x64.
- A global error handler. Unexpected errors are written to `application.log` and shown in a message box, and the app stays open instead of closing silently.

### Changed
- UWPHook now runs on .NET 10 as a self-contained single-file app. No separate .NET runtime install is needed.
- App discovery runs the built-in Windows PowerShell 5.1 in a separate process instead of a bundled PowerShell 7. The app no longer changes your PowerShell execution policy.
- Steam shortcuts now point at a launcher path that stays the same across updates (`%LocalAppData%\UWPHook\UWPHook.exe` for installed copies), so updating UWPHook doesn't break or duplicate your shortcuts.
- Artwork from SteamGridDB is saved exactly as downloaded (PNG, JPEG or WebP) instead of being re-encoded.
- The "Change resolution" option was removed. It did not work on desktop Windows (#124), and it will return in 3.0.
- The NSIS installer was replaced by the Velopack installer.

### Fixed
- **The app list is empty, or shows "Error trying to get installed apps"** (the Appx module could not load in the bundled PowerShell 7): #141 #149 #155 #164 #171 #178 #181 (also #132).
- **"Value does not fall within the expected range" when launching from Steam**, and games not starting when UWPHook is in a folder with spaces: #157 #160 #168.
  - Shortcuts now quote their `Exe` and `Start In` paths.
  - Shortcuts created by older versions launch again without being re-exported.
- **Exporting no longer creates duplicate shortcuts or orphans your artwork and playtime.** Re-exporting updates UWPHook's existing entry in place, matched by the app's AUMID, and keeps its Steam app id. Exporting never changes or removes shortcuts created by other tools.
- **Export hangs or closes the app** (#166 #176, also #80 #143 #158).
  - Restarting Steam now gives up after 15 seconds instead of waiting forever. If it gives up, UWPHook asks you to restart Steam yourself.
  - Errors during export are reported instead of closing the app.
- **The settings window crashed on Save** (#162).
- **"An item with the same key has already been added" when reading `shortcuts.vdf`** (#139).
- **Forza Motorsport and other games with localized names were missing from the list** (#127). Thanks to @icavalheiro (PR #169).
- **Different games from the same publisher were hidden as duplicates** (for example Hollow Knight and Hollow Knight: Silksong). Thanks to @RXA7 (PR #182).
- A game launched without an executable name is no longer matched to an unrelated running process.
- Artwork downloaded in a different image format replaces the old file instead of leaving a stale copy next to it.

<!-- 8b/9+ entries added at merge -->

[2.15.0]: https://github.com/BrianLima/UWPHook/compare/2.14.3...v2.15.0
