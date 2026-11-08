# UWPHook

[![](http://imgur.com/gWwR02D.png)](https://briano.dev/UWPHook/)

Small project to link UWP games and XboxGamePass to Steam

If you want to add Windows Store or Xbox Game Pass Games to Steam, you need to do a bit of a workaround because Steam can't see UWP apps, and there's a chance Steam won't show it on your "Currently playing" status. This app aims to simplify a little bit the process where it is possible by automating the scripting and launching of Windows Store apps and Xbox Game Pass games.

# To add UWP or XGP games to Steam #

Get UWPHook from the [latest release](https://github.com/BrianLima/UWPHook/releases/latest). Pick the files for your PC: `win-x64` for most PCs, `win-arm64` for ARM devices (Snapdragon and similar).

- **Installer (recommended):** download and run `UWPHook-win-x64-Setup.exe` (or `UWPHook-win-arm64-Setup.exe`). It installs UWPHook for your Windows user only, into `%LocalAppData%\UWPHook`, without asking for administrator rights, and adds Start menu and desktop shortcuts. Updates keep your Steam shortcuts working because they always point at `%LocalAppData%\UWPHook\UWPHook.exe`.
- **Portable:** download `UWPHook-win-x64-Portable.zip` (or the `win-arm64` one), extract it to a folder that you won't move later (your Steam shortcuts point at it), and run `UWPHook.exe`.

The `SHA256SUMS.txt` file in each release lists the checksums of every download.

Upgrading from 2.14 or older: install 2.15 as above, then export your games again. UWPHook updates its existing Steam shortcuts in place, so your playtime, artwork and app ids are kept. You can delete the old UWPHook folder afterwards (or uninstall the old version); your settings, backups and logs live in `%AppData%\Briano\UWPHook` and are not touched.

Click on the 🔄 to load installed UWP Apps, we will find every UWP app and Xbox Game Pass game installed on your PC.

![](https://i.imgur.com/pjGfGHe.png)

Select every app you want to add to Steam, you can change the name by double clicking the "name" collumn, press "Export selected apps to Steam" and 🎉, every app you selected will be added to Steam.

![](https://i.imgur.com/on46CMQ.png)

Close UWPHook, restart Steam if prompted, click play on your UWP game, and Steam will show your current game on your status as long as you are playing it!

----------

# SteamGridDB #

Since v2.8, UWPHook can automatically import grid, icons and hero images from [SteamGridDB](https://www.steamgriddb.com)

On your first usage, the app will ask you if you want it to download images, redirecting you to the settings page.

![](https://i.imgur.com/K0Cm4IL.png)

By adding a API Key obtained in the SteamGridDB preferences, UWPHook will try to find matching images for any exported games, giving you the following result:

![](https://i.imgur.com/mlvVdwb.png)

You can refine the images by using filters for animated images, blurred, no logo or memes for example, but it will always pick the first it finds for the filters automatically.

Special thanks to @FusRoDah061 for implementing the base feature!

# Troubleshooting #

- **Steam's Overlay isn't working!**
  - Unfortunately, it's a Steam limitation, Valve has to update it in order to work properly with UWP, DXTory is a recommended overlay for UWP games.
- **Using Steam Link**
  - Check the option "Streaming" mode inside the settings screen
- **Steam Deck?**
  - This app is not compatible with the Steam Deck in any way.
- **"Value does not fall within the expected range", or the game doesn't start from Steam**
  - Older versions wrote Steam shortcuts without quotes, which broke when UWPHook lived in a folder with spaces in its name (for example `C:\Program Files\...`). Since 2.15, shortcuts are quoted and old shortcuts are understood too. Open UWPHook and export your games again to refresh them.
- **My exported games don't show up in Steam**
  - Steam only reads its shortcuts when it starts. UWPHook restarts Steam for you after an export; if Steam doesn't close within 15 seconds, UWPHook leaves it running and asks you to restart Steam yourself.
- **The app list is empty or says "Error trying to get installed apps"**
  - UWPHook asks Windows PowerShell (`powershell.exe`, built into Windows) for your installed apps. Make sure it isn't blocked by antivirus or group policy, then check `application.log` (see below) for the error.
- **I have shortcuts from other application that broke when i used UWPHook**
  - Since 2.15, exporting only adds or updates UWPHook's own shortcuts and leaves every other shortcut as it was.
  - Before every change, UWPHook backs up your `shortcuts.vdf` to `%AppData%\Briano\UWPHook\backups`. Each file is named `{userid}_{yyyyMMddHHmmss}_shortcuts.vdf`. To restore one, close Steam, copy it over `<Steam folder>\userdata\{userid}\config\shortcuts.vdf`, and start Steam again.
  - Uninstalling UWPHook does not delete this folder, so your backups, logs and settings stay where they are.
- **My question isn't listed here!**
  - Drop by our subreddit and ask a question over there, maybe someone will help you, i surely will as soon as i can:
 **[https://www.reddit.com/r/uwphook](https://www.reddit.com/r/uwphook)**
  - Please also paste the contents of the file `%AppData%\Briano\UWPHook\application.log` so i can try to understand better the problem.
----------

# Building

- Clone the project (or your fork). No other repositories are needed: the VDF parser is vendored in `VDFParser/`.
- Install [Visual Studio 2026](https://visualstudio.microsoft.com/) with the ".NET desktop development" workload, **or** just the [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0). The SDK version is pinned in `global.json`.
- Build from the repository root:
  ```
  dotnet build UWPHook.slnx
  ```
- Publish a self-contained, single-file `UWPHook.exe` (use `win-arm64` for ARM devices). The output folder holds `UWPHook.exe` plus the native runtime DLLs it loads from the same folder; nothing is extracted to `%TEMP%`:
  ```
  dotnet publish UWPHook/UWPHook.csproj -c Release -r win-x64
  ```
- Every push is built by GitHub Actions (`.github/workflows/ci.yml`), which uploads the `win-x64` and `win-arm64` builds as artifacts.

## Installer and releases

The installer, the portable zip and the update feed are built with [Velopack](https://velopack.io) by `.github/workflows/release.yml`:

- Pushing a tag `v<version>` (it must match `<Version>` in `Directory.Build.props`) publishes `win-x64` and `win-arm64`, packs each one with `vpk` on its own channel, and uploads everything, plus `SHA256SUMS.txt`, to a **draft** GitHub Release for review.
- Running the workflow by hand only builds and packs, and keeps the files as a workflow artifact.
- CI runs the same packing script (`.github/scripts/pack-release.ps1`) on every push, so packing problems show up before release day.

To pack locally, install the `vpk` tool (`dotnet tool install -g vpk`, the same version as the `Velopack` package in `Directory.Packages.props`), publish as above into `publish/<rid>`, then run `pwsh .github/scripts/pack-release.ps1 -Version <version>`.

----------

# About

This software is open-source under the MIT License.
It will mostly likely break withouth any heads up, since any API, file format, script and many other things used by it may be changed by Valve or Microsoft withouth prior notice.

I am not responsible if anything breaks.

If you like what i did with it and want to suport me, you can cheer me up at my [Twitter](http://www.twitter.com/brianostorm "Twitter") or [pay me a coffee via Paypal, it will help me to continue to build amazing open source tools for you!"](https://www.paypal.com/cgi-bin/webscr?cmd=_s-xclick&hosted_button_id=9YPV3FHEFRAUQ)

Thanks for your support, and game on!
