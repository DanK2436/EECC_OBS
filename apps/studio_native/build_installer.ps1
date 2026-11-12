# Script de compilation locale de l'installateur Windows EECC Studio
# Usage : .\build_installer.ps1

Write-Host "==========================================================" -ForegroundColor Cyan
Write-Host "   COMPILATION EECC STUDIO C# / C++ & INSTALLATEUR" -ForegroundColor Cyan
Write-Host "==========================================================" -ForegroundColor Cyan

$PublishDir = "$PSScriptRoot\publish\win-x64"
$DistDir = "$PSScriptRoot\dist_installer"
$ProjectFile = "$PSScriptRoot\src\EeccStudio.App\EeccStudio.App.csproj"
$IssFile = "$PSScriptRoot\installer\EeccStudio_Setup.iss"

# 1. Nettoyage des dossiers précédents
if (Test-Path $PublishDir) { Remove-Item $PublishDir -Recurse -Force }
if (-not (Test-Path $DistDir)) { New-Item -ItemType Directory -Path $DistDir -Force }

# 2. Publication .NET 8 Native Self-Contained (Win-x64)
Write-Host "`n[1/3] Publication de l'application .NET 8 (Self-contained x64)..." -ForegroundColor Yellow
dotnet publish $ProjectFile `
    -c Release `
    -r win-x64 `
    --self-contained true `
    -p:PublishSingleFile=false `
    -p:IncludeNativeLibrariesForSelfExtract=true `
    -o $PublishDir

if ($LASTEXITCODE -ne 0) {
    Write-Host "Erreur lors de la compilation dotnet." -ForegroundColor Red
    exit 1
}

# 3. Recherche du compilateur Inno Setup (ISCC.exe)
Write-Host "`n[2/3] Recherche du compilateur Inno Setup..." -ForegroundColor Yellow
$InnoCandidates = @(
    "C:\Program Files (x86)\Inno Setup 6\ISCC.exe",
    "C:\Program Files\Inno Setup 6\ISCC.exe",
    (Get-Command iscc -ErrorAction SilentlyContinue | Select-Object -ExpandProperty Source)
)

$IsccPath = $InnoCandidates | Where-Object { $_ -and (Test-Path $_) } | Select-Object -First 1

if ($IsccPath) {
    Write-Host "Compilateur trouvé : $IsccPath" -ForegroundColor Green
    Write-Host "`n[3/3] Génération de l'installateur EECC_Studio_Setup_x64.exe..." -ForegroundColor Yellow
    & $IsccPath $IssFile
    Write-Host "`nSUCCÈS ! L'installateur est prêt dans : $DistDir\EECC_Studio_Setup_x64.exe" -ForegroundColor Green
} else {
    Write-Host "`n[INFO] Inno Setup n'est pas détecté en local sur cette machine." -ForegroundColor Yellow
    Write-Host "L'application a été publiée avec succès dans : $PublishDir" -ForegroundColor Green
    Write-Host "Pour générer l'installateur .exe automatiquement, poussez sur GitHub (le workflow GitHub Actions s'en chargera avec Inno Setup inclus)." -ForegroundColor Cyan
}
