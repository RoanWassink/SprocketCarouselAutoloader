$ErrorActionPreference = 'Stop'
if (Get-Process Sprocket -ErrorAction SilentlyContinue) { throw 'Sluit Sprocket voordat je de carousel-plugin installeert.' }
$carouselRoot = (Resolve-Path -LiteralPath $PSScriptRoot).Path
$carouselGame = (Resolve-Path -LiteralPath (Join-Path $carouselRoot '..\..')).Path
$carouselSource = Join-Path $carouselRoot 'bin\Release\net6.0\SprocketCarouselAutoloader.dll'
$carouselTarget = Join-Path $carouselGame 'BepInEx\plugins\SprocketCarouselAutoloader.dll'
if (!(Test-Path -LiteralPath $carouselSource)) { throw 'Bouw eerst CarouselAutoloader.csproj -c Release.' }
$bustleSource = Join-Path $carouselRoot 'Parts\roanBustleAutoloaderPart.json'
$bustleTarget = Join-Path $carouselGame 'Sprocket_Data\StreamingAssets\Parts\roanBustleAutoloaderPart.json'
if (!(Test-Path -LiteralPath $bustleSource)) { throw 'Bustle part definition is missing.' }
if (Test-Path -LiteralPath $carouselTarget) {
    $carouselBackup = Join-Path $carouselRoot ('backups\' + (Get-Date -Format 'yyyyMMdd-HHmmss-fff'))
    New-Item -ItemType Directory -Path $carouselBackup -Force | Out-Null
    Copy-Item -LiteralPath $carouselTarget -Destination $carouselBackup
    if (Test-Path -LiteralPath $bustleTarget) { Copy-Item -LiteralPath $bustleTarget -Destination $carouselBackup }
}
Copy-Item -LiteralPath $bustleSource -Destination $bustleTarget
Copy-Item -LiteralPath $carouselSource -Destination $carouselTarget
Get-FileHash -LiteralPath $carouselTarget -Algorithm SHA256
