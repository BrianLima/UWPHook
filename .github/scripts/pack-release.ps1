# Packs already-published UWPHook builds with Velopack (vpk) and gathers the release assets.
# Used by release.yml (tag builds) and by ci.yml (a smoke run on every push, so packing never
# breaks silently between releases).
#
#   pwsh .github/scripts/pack-release.ps1 -Version 2.15.0 [-PublishRoot publish] [-OutputRoot releases]
#
# Expects <PublishRoot>/<rid>/UWPHook.exe for each runtime (dotnet publish -r <rid> -o publish/<rid>).
# Writes each runtime's vpk output to <OutputRoot>/<rid>, then copies the files to upload into
# <OutputRoot>/upload together with SHA256SUMS.txt.
param(
    [Parameter(Mandatory)][string]$Version,
    [string]$PublishRoot = 'publish',
    [string]$OutputRoot = 'releases',
    [string[]]$Rids = @('win-x64', 'win-arm64')
)

$ErrorActionPreference = 'Stop'

$upload = Join-Path $OutputRoot 'upload'
New-Item -ItemType Directory -Force -Path $upload | Out-Null

foreach ($rid in $Rids) {
    $out = Join-Path $OutputRoot $rid
    # One channel per runtime: Velopack needs a distinct channel for every os/arch it ships,
    # and an installed app only looks for updates in the channel it was packed with.
    vpk pack `
        --packId UWPHook `
        --packVersion $Version `
        --packTitle UWPHook `
        --packAuthors 'Brian Lima' `
        --packDir (Join-Path $PublishRoot $rid) `
        --mainExe UWPHook.exe `
        --icon UWPHook/Resources/hook2.ico `
        --runtime $rid `
        --channel $rid `
        --outputDir $out
    if ($LASTEXITCODE -ne 0) { throw "vpk pack failed for $rid (exit code $LASTEXITCODE)" }

    # Upload the installer, the portable zip, the packages and the releases.<channel>.json feed.
    # Skip the legacy RELEASES file (only for apps migrating from Squirrel, which UWPHook never used)
    # and assets.<channel>.json (vpk's local list of build outputs).
    $assets = Get-ChildItem $out -File | Where-Object { $_.Name -notlike 'RELEASES*' -and $_.Name -notlike 'assets.*.json' }
    foreach ($file in $assets) {
        $target = Join-Path $upload $file.Name
        if (Test-Path $target) { throw "Two runtimes produced $($file.Name); their release assets would overwrite each other." }
        Copy-Item $file.FullName $target
    }
}

$sums = Get-ChildItem $upload -File | Where-Object Name -ne 'SHA256SUMS.txt' | Sort-Object Name | ForEach-Object {
    "$((Get-FileHash $_.FullName -Algorithm SHA256).Hash.ToLowerInvariant())  $($_.Name)"
}
Set-Content -Path (Join-Path $upload 'SHA256SUMS.txt') -Value $sums -Encoding ascii

Get-ChildItem $upload -File | ForEach-Object { "$($_.Name) ($([math]::Round($_.Length / 1MB, 1)) MB)" }
