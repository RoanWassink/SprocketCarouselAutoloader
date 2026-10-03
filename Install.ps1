$ErrorActionPreference = 'Stop'
if (Get-Process Sprocket -ErrorAction SilentlyContinue) { throw 'Sluit Sprocket voordat je de carousel-plugin installeert.' }
$carouselRoot = (Resolve-Path -LiteralPath $PSScriptRoot).Path
$carouselGame = (Resolve-Path -LiteralPath (Join-Path $carouselRoot '..\..')).Path
$carouselSource = Join-Path $carouselRoot 'bin\Release\net6.0\SprocketCarouselAutoloader.dll'
$carouselTarget = Join-Path $carouselGame 'BepInEx\plugins\SprocketCarouselAutoloader.dll'
if (!(Test-Path -LiteralPath $carouselSource)) { throw 'Bouw eerst CarouselAutoloader.csproj -c Release.' }
if (Test-Path -LiteralPath $carouselTarget) {
    $carouselBackup = Join-Path $carouselRoot ('backups\' + (Get-Date -Format 'yyyyMMdd-HHmmss-fff'))
    New-Item -ItemType Directory -Path $carouselBackup -Force | Out-Null
    Copy-Item -LiteralPath $carouselTarget -Destination $carouselBackup
}
Copy-Item -LiteralPath $carouselSource -Destination $carouselTarget
Get-FileHash -LiteralPath $carouselTarget -Algorithm SHA256
