# Construit un dossier Windows autonome : programme, ffmpeg et secret de régie.
# Ce paquet se copie sur une clé ou un autre PC. Il ne dépend pas du profil de cette machine.
$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
Set-Location $root

& (Join-Path $PSScriptRoot "fetch-ffmpeg.ps1")
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

$secret = Join-Path $PSScriptRoot "studio.secret"
if (-not (Test-Path $secret)) {
    $appData = Join-Path $env:APPDATA "EECC_Studio\studio.secret"
    if (Test-Path $appData) {
        Copy-Item $appData $secret
    }
}
if (-not (Test-Path $secret) -or (Get-Item $secret).Length -lt 16) {
    throw "studio.secret est absent. Sans lui, une autre machine ne peut pas publier."
}

$dotnet = "C:\Program Files\dotnet\dotnet.exe"
if (-not (Test-Path $dotnet)) { $dotnet = "dotnet" }
& $dotnet publish src/EeccStudio.App/EeccStudio.App.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=false -p:IncludeNativeLibrariesForSelfExtract=true -o publish/win-x64
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

$dest = Join-Path $root "publish\win-x64"
$ff = Join-Path $dest "ffmpeg"
New-Item -ItemType Directory -Force -Path $ff | Out-Null
Copy-Item (Join-Path $root "third_party\ffmpeg\ffmpeg.exe") (Join-Path $ff "ffmpeg.exe") -Force
Copy-Item (Join-Path $root "third_party\ffmpeg\ffprobe.exe") (Join-Path $ff "ffprobe.exe") -Force
Get-ChildItem (Join-Path $root "third_party\ffmpeg") -Filter "LICENSE*" -ErrorAction SilentlyContinue | Copy-Item -Destination $ff -Force
Copy-Item $secret (Join-Path $dest "studio.secret") -Force

$outDir = Join-Path $root "dist_installer"
New-Item -ItemType Directory -Force -Path $outDir | Out-Null
$zip = Join-Path $outDir "EECC_Studio_Portable_win-x64.zip"
if (Test-Path $zip) { Remove-Item $zip -Force }
Compress-Archive -Path (Join-Path $dest "*") -DestinationPath $zip -Force

$desktop = Join-Path ([Environment]::GetFolderPath("Desktop")) "EECC_Studio_Portable_win-x64.zip"
Copy-Item $zip $desktop -Force
Write-Host "PAQUET=$zip"
Write-Host "BUREAU=$desktop"
