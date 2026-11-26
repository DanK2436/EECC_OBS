# Télécharge ffmpeg (build essentials Windows x64) pour l'installateur EECC Studio.
# Les binaires ne sont pas versionnés : ce script les dépose dans third_party/ffmpeg.

$ErrorActionPreference = "Stop"
$dest = Join-Path (Split-Path -Parent $PSScriptRoot) "third_party\ffmpeg"
New-Item -ItemType Directory -Force -Path $dest | Out-Null

$ffmpeg = Join-Path $dest "ffmpeg.exe"
$ffprobe = Join-Path $dest "ffprobe.exe"
if ((Test-Path $ffmpeg) -and (Test-Path $ffprobe)) {
    Write-Host "ffmpeg déjà présent : $dest"
    exit 0
}

$urls = @(
    "https://github.com/BtbN/FFmpeg-Builds/releases/download/latest/ffmpeg-master-latest-win64-gpl.zip",
    "https://www.gyan.dev/ffmpeg/builds/ffmpeg-release-essentials.zip"
)
$zip = Join-Path $env:TEMP "eecc-ffmpeg-essentials.zip"
$extract = Join-Path $env:TEMP "eecc-ffmpeg-extract"
$downloaded = $false
foreach ($url in $urls) {
    Write-Host "Téléchargement de ffmpeg depuis $url"
    if (Test-Path $zip) { Remove-Item $zip -Force }
    & curl.exe -L --fail --retry 3 --retry-all-errors --connect-timeout 30 --max-time 600 -o $zip $url
    if ($LASTEXITCODE -eq 0 -and (Test-Path $zip) -and (Get-Item $zip).Length -gt 1MB) {
        $downloaded = $true
        break
    }
}
if (-not $downloaded) { throw "Le téléchargement de ffmpeg a échoué." }
if (Test-Path $extract) { Remove-Item $extract -Recurse -Force }
Expand-Archive -Path $zip -DestinationPath $extract -Force

$found = Get-ChildItem -Path $extract -Recurse -Filter "ffmpeg.exe" | Select-Object -First 1
if ($null -eq $found) { throw "ffmpeg.exe est absent de l'archive." }
$bin = $found.DirectoryName
Get-ChildItem -Path $bin -File | Where-Object { $_.Extension -match '^\.(exe|dll)$' } | ForEach-Object {
    Copy-Item $_.FullName (Join-Path $dest $_.Name) -Force
}
if (-not (Test-Path $ffprobe)) { throw "ffprobe.exe est absent de l'archive." }
Get-ChildItem -Path $extract -Recurse -File | Where-Object { $_.Name -like "LICENSE*" -or $_.Name -eq "COPYING*" } | Select-Object -First 1 | ForEach-Object {
    Copy-Item $_.FullName (Join-Path $dest $_.Name) -Force
}
Write-Host "ffmpeg installé dans $dest"
