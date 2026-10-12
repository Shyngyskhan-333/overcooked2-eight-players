<#
  Overcooked! 2 Eight-Player Mod - uninstaller / recovery.
  Usage:   powershell -ExecutionPolicy Bypass -File uninstall.ps1 -GameDir "C:\path\to\Overcooked!_2" [-KeepBepInEx] [-RestoreSaves]

  Reads <GameDir>\_oc2_8p_backup\install-manifest.json and undoes exactly what install.ps1 did:
    * deletes the mod DLLs, the mod config and the files the mod generated (diagnostic .txt files, UI screenshots),
    * deletes the files install.ps1 created (BepInEx, if install.ps1 installed it), restores any file it replaced from the backup,
    * removes folders that are now empty, then removes the backup folder.
  Anything you added yourself (other plugins, other configs) is left alone. Because of that, a BepInEx folder that still holds
  other files is kept.
  Your saves are not touched. -RestoreSaves copies the save backup taken at install time back over the live save folder.
  -KeepBepInEx keeps BepInEx itself and only removes this mod.
  Fastest manual disable without this script: rename <GameDir>\winhttp.dll to winhttp.dll.off (the game then starts vanilla).
#>
param(
    [Parameter(Mandatory = $true)][string]$GameDir,
    [switch]$KeepBepInEx,
    [switch]$RestoreSaves
)
$ErrorActionPreference = 'Stop'
$GameDir = (Resolve-Path $GameDir).Path
$backup = Join-Path $GameDir '_oc2_8p_backup'
$manifestPath = Join-Path $backup 'install-manifest.json'
if (-not (Test-Path $manifestPath)) { throw "No install manifest at $manifestPath - nothing to uninstall." }
if (Get-Process Overcooked2 -ErrorAction SilentlyContinue | Where-Object { $_.Path -like "$GameDir*" }) { throw "Close Overcooked! 2 first." }
$m = Get-Content $manifestPath -Raw | ConvertFrom-Json

function Remove-IfPresent([string]$rel) {
    $p = Join-Path $GameDir $rel
    if (Test-Path $p) { Remove-Item $p -Recurse -Force; Write-Host "removed $rel" }
}

# 1. The mod itself and everything it generated at run time.
foreach ($rel in 'BepInEx\patchers\OC2.EightPlayers.Patcher.dll', 'BepInEx\plugins\OC2.EightPlayers.dll', 'BepInEx\config\local.oc2.eightplayers.cfg',
                 'BepInEx\8P-rewritten.txt', 'BepInEx\8P-unresolved.txt', 'BepInEx\8p-sim.txt', 'BepInEx\ui-shots') { Remove-IfPresent $rel }

# 2. BepInEx and the files install.ps1 put there.
if (-not $KeepBepInEx) {
    foreach ($rel in @($m.created)) { Remove-IfPresent $rel }
    foreach ($r in @($m.replaced)) {
        $src = Join-Path $backup ('files\' + $r.path)
        if (Test-Path $src) { Copy-Item $src (Join-Path $GameDir $r.path) -Force; Write-Host "restored $($r.path)" }
    }
    if ($m.bepInExInstalledByUs) {
        # files BepInEx itself generates on first start
        foreach ($rel in 'BepInEx\LogOutput.log', 'BepInEx\config\BepInEx.cfg', 'BepInEx\cache') { Remove-IfPresent $rel }
    }
} else {
    Write-Host 'Kept BepInEx (-KeepBepInEx).'
}

# 3. Prune empty BepInEx folders (never deletes a folder that still has files in it).
$bep = Join-Path $GameDir 'BepInEx'
if (Test-Path $bep) {
    Get-ChildItem $bep -Recurse -Directory | Sort-Object { $_.FullName.Length } -Descending |
        Where-Object { -not (Get-ChildItem $_.FullName -Force) } | ForEach-Object { Remove-Item $_.FullName -Force }
    if (-not (Get-ChildItem $bep -Force)) { Remove-Item $bep -Force }
    else { Write-Host 'BepInEx folder kept: it still contains files that are not part of this mod.' }
}

if ($RestoreSaves) {
    $saves = Join-Path $env:USERPROFILE 'AppData\LocalLow\Team17\Overcooked2\Saves'
    if (Test-Path (Join-Path $backup 'Saves')) { Copy-Item (Join-Path $backup 'Saves\*') $saves -Recurse -Force; Write-Host 'Saves restored from backup.' }
}
Remove-Item $backup -Recurse -Force
Write-Host 'Uninstalled.'
