# Manual Windows checklist

Checks that need a real Windows machine (they cannot run in CI). Tick them off on a Windows box before a release.

## Task 1
- [ ] The published `UWPHook.exe` (CI artifact `UWPHook-win-x64`) opens, and Settings shows the version `2.15.0.0`.
- [ ] The publish folder contains a single `UWPHook.exe` and running it does not extract files to a temp folder.
- [ ] `%AppData%\Briano\UWPHook\application.log` is still written after launching the app.
