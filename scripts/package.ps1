<#
  Builds the release zip: dist\OC2-EightPlayers-<version>.zip
  Usage:   powershell -ExecutionPolicy Bypass -File scripts\package.ps1 -GameDir "C:\path\to\Overcooked!_2" [-Version 0.1.0]
  The zip contains only this project's own files: the two DLLs, install.ps1, uninstall.ps1, README.md, LICENSE, CHANGELOG.md.
  It contains no game files and no BepInEx (install.ps1 downloads BepInEx from its official release and verifies its SHA-256).
#>
param(
    [Parameter(Mandatory = $true)][string]$GameDir,
    [string]$Version = '0.1.0',
    [string]$BepInExCore = ''
)
$ErrorActionPreference = 'Stop'
$here = Split-Path -Parent $MyInvocation.MyCommand.Path
$root = Split-Path -Parent $here

& (Join-Path $here 'build.ps1') -GameDir $GameDir -BepInExCore $BepInExCore

$stage = Join-Path $root 'dist\stage'
Remove-Item $stage -Recurse -Force -ErrorAction SilentlyContinue
New-Item -ItemType Directory -Force "$stage\BepInEx\patchers", "$stage\BepInEx\plugins" | Out-Null
Copy-Item (Join-Path $root 'dist\bin\OC2.EightPlayers.Patcher.dll') "$stage\BepInEx\patchers\"
Copy-Item (Join-Path $root 'dist\bin\OC2.EightPlayers.dll') "$stage\BepInEx\plugins\"
foreach ($f in 'install.ps1', 'uninstall.ps1') { Copy-Item (Join-Path $here $f) $stage }
foreach ($f in 'README.md', 'LICENSE', 'CHANGELOG.md') { Copy-Item (Join-Path $root $f) $stage }

$zip = Join-Path $root "dist\OC2-EightPlayers-$Version.zip"
Remove-Item $zip -Force -ErrorAction SilentlyContinue
Add-Type -AssemblyName System.IO.Compression, System.IO.Compression.FileSystem
# Build the archive by hand so entry names use forward slashes (Windows PowerShell 5.1 would write backslashes).
$archive = [System.IO.Compression.ZipFile]::Open($zip, 'Create')
try {
    Get-ChildItem $stage -Recurse -File | ForEach-Object {
        $name = $_.FullName.Substring($stage.Length).TrimStart('\', '/').Replace('\', '/')
        [void][System.IO.Compression.ZipFileExtensions]::CreateEntryFromFile($archive, $_.FullName, $name, 'Optimal')
    }
} finally { $archive.Dispose() }
Remove-Item $stage -Recurse -Force

$h = (Get-FileHash $zip -Algorithm SHA256).Hash
Write-Host "`n$zip"
Write-Host "SHA-256: $h"
