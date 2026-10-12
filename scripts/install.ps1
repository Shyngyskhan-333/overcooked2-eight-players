<#
  Overcooked! 2 Eight-Player Mod - installer (Windows, PowerShell 5.1+).
  Usage:   powershell -ExecutionPolicy Bypass -File install.ps1 -GameDir "C:\path\to\Overcooked!_2" [-DryRun]
  What it does (and ONLY this):
    1. Verifies GameDir contains Overcooked2.exe and Overcooked2_Data\Managed\Assembly-CSharp.dll.
    2. Backs up every existing file it would overwrite into <GameDir>\_oc2_8p_backup\ (with SHA256 manifest).
    3. Downloads BepInEx 5.4.23.5 x64 from the official GitHub release and verifies its SHA256 (BepInEx is NOT bundled here).
       If BepInEx is already installed it is left untouched (winhttp.dll / doorstop_config.ini are never overwritten).
    4. Copies BepInEx\patchers\OC2.EightPlayers.Patcher.dll and BepInEx\plugins\OC2.EightPlayers.dll.
    5. Writes <GameDir>\_oc2_8p_backup\install-manifest.json so uninstall.ps1 can undo exactly this.
  It never modifies Overcooked2.exe or any file under Overcooked2_Data.
#>
param(
    [Parameter(Mandatory = $true)][string]$GameDir,
    [switch]$DryRun
)
$ErrorActionPreference = 'Stop'
$here = Split-Path -Parent $MyInvocation.MyCommand.Path
$bepUrl = 'https://github.com/BepInEx/BepInEx/releases/download/v5.4.23.5/BepInEx_win_x64_5.4.23.5.zip'
$bepSha = '82F9878551030F54657792C0740D9D51A09500EEAE1FBA21106B0C441E6732C4'

$GameDir = (Resolve-Path $GameDir).Path
if (-not (Test-Path "$GameDir\Overcooked2.exe")) { throw "Overcooked2.exe not found in $GameDir" }
if (-not (Test-Path "$GameDir\Overcooked2_Data\Managed\Assembly-CSharp.dll")) { throw "Overcooked2_Data\Managed\Assembly-CSharp.dll not found in $GameDir" }
if (Get-Process Overcooked2 -ErrorAction SilentlyContinue | Where-Object { $_.Path -like "$GameDir*" }) { throw "Close Overcooked! 2 first." }

$patcherSrc = Join-Path $here 'BepInEx\patchers\OC2.EightPlayers.Patcher.dll'
$pluginSrc  = Join-Path $here 'BepInEx\plugins\OC2.EightPlayers.dll'
foreach ($f in $patcherSrc, $pluginSrc) { if (-not (Test-Path $f)) { throw "Package file missing: $f" } }

$backup = Join-Path $GameDir '_oc2_8p_backup'
$manifestPath = Join-Path $backup 'install-manifest.json'
if (Test-Path $manifestPath) { throw "Already installed (found $manifestPath). Run uninstall.ps1 first." }

$haveBepInEx = Test-Path "$GameDir\winhttp.dll"
$bepInExIsOurs = $false
Write-Host "Game folder : $GameDir"
Write-Host ("BepInEx     : " + $(if ($haveBepInEx) { 'already present (will be reused, not modified)' } else { 'not present (will be downloaded and installed)' }))
if ($DryRun) { Write-Host 'DryRun: nothing changed.'; return }

New-Item -ItemType Directory -Force $backup | Out-Null
$created = New-Object System.Collections.ArrayList     # files we created (to delete on uninstall)
$backedUp = New-Object System.Collections.ArrayList    # files we replaced (original saved in backup)

function Save-Original([string]$path) {
    if (Test-Path $path) {
        $rel = $path.Substring($GameDir.Length).TrimStart('\')
        $dest = Join-Path $backup ('files\' + $rel)
        New-Item -ItemType Directory -Force (Split-Path $dest) | Out-Null
        Copy-Item $path $dest -Force
        [void]$backedUp.Add([ordered]@{ path = $rel; sha256 = (Get-FileHash $path -Algorithm SHA256).Hash })
    }
}
function Put-File([string]$src, [string]$dst) {
    Save-Original $dst
    $existed = Test-Path $dst
    New-Item -ItemType Directory -Force (Split-Path $dst) | Out-Null
    Copy-Item $src $dst -Force
    if (-not $existed) { [void]$created.Add($dst.Substring($GameDir.Length).TrimStart('\')) }
}

if (-not $haveBepInEx) {
    $zip = Join-Path $env:TEMP 'BepInEx_win_x64_5.4.23.5.zip'
    Write-Host "Downloading BepInEx 5.4.23.5 ..."
    Invoke-WebRequest $bepUrl -OutFile $zip -UseBasicParsing
    $h = (Get-FileHash $zip -Algorithm SHA256).Hash
    if ($h -ne $bepSha) { Remove-Item $zip -Force; throw "BepInEx download hash mismatch ($h). Aborting; nothing was installed." }
    $tmp = Join-Path $env:TEMP ('bepinex_' + [guid]::NewGuid().ToString('N'))
    Add-Type -AssemblyName System.IO.Compression.FileSystem
    [System.IO.Compression.ZipFile]::ExtractToDirectory($zip, $tmp)
    Get-ChildItem $tmp -Recurse -File | ForEach-Object {
        $rel = $_.FullName.Substring($tmp.Length).TrimStart('\')
        Put-File $_.FullName (Join-Path $GameDir $rel)
    }
    Remove-Item $tmp -Recurse -Force; Remove-Item $zip -Force
    $bepInExIsOurs = $true
}

Put-File $patcherSrc (Join-Path $GameDir 'BepInEx\patchers\OC2.EightPlayers.Patcher.dll')
Put-File $pluginSrc  (Join-Path $GameDir 'BepInEx\plugins\OC2.EightPlayers.dll')

# Back up the save folder once (hash list) - the mod does not change the save format, but this is cheap insurance.
$saves = Join-Path $env:USERPROFILE 'AppData\LocalLow\Team17\Overcooked2\Saves'
if (Test-Path $saves) { Copy-Item $saves (Join-Path $backup 'Saves') -Recurse -Force }

[ordered]@{
    version = '0.1.0'; installedAt = (Get-Date).ToString('s'); gameDir = $GameDir; bepInExInstalledByUs = $bepInExIsOurs
    created = $created; replaced = $backedUp
} | ConvertTo-Json -Depth 5 | Set-Content $manifestPath -Encoding UTF8

Write-Host ''
Write-Host "Installed. Backup + manifest: $backup"
Write-Host 'Start the game normally. First start creates BepInEx\LogOutput.log and BepInEx\config\local.oc2.eightplayers.cfg.'
Write-Host 'To remove everything: powershell -ExecutionPolicy Bypass -File uninstall.ps1 -GameDir "<same path>"'
