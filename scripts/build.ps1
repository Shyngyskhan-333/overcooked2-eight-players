<#
  Builds the patcher and the plugin from source, then checks the patcher offline against your game's pristine Assembly-CSharp.dll.

  Usage:   powershell -ExecutionPolicy Bypass -File scripts\build.ps1 -GameDir "C:\path\to\Overcooked!_2"
  Needs:   .NET SDK 8 or newer (dotnet on PATH), BepInEx 5.4.23.5 x64 unpacked in <GameDir> (scripts\install.ps1 does that,
           or pass -BepInExCore to point at the BepInEx\core folder of an unpacked BepInEx zip).
  Output:  dist\bin\OC2.EightPlayers.Patcher.dll and dist\bin\OC2.EightPlayers.dll

  The offline check applies every IL patch to a copy of Assembly-CSharp.dll in memory, writes the result to a temp folder and reloads it.
  Any "PATCH FAILURE" line means a patch did not match your game build; the script then exits with an error.
  Nothing is written to the game folder.
#>
param(
    [Parameter(Mandatory = $true)][string]$GameDir,
    [string]$BepInExCore = '',
    [string]$Configuration = 'Release'
)
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent (Split-Path -Parent $MyInvocation.MyCommand.Path)
$GameDir = (Resolve-Path $GameDir).Path
if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) { throw '.NET SDK not found. Install the .NET 8 SDK from https://dotnet.microsoft.com/download and reopen the shell.' }
$sdks = & dotnet --list-sdks
if (-not $sdks) { throw 'dotnet is installed but no SDK was found (only a runtime). Install the .NET 8 SDK.' }

$props = @("/p:GameDir=$GameDir", '/nologo', '-v:q')
if ($BepInExCore) { $props += "/p:BepInExCore=$((Resolve-Path $BepInExCore).Path)" }

foreach ($proj in 'Patcher\OC2.EightPlayers.Patcher.csproj', 'Plugin\OC2.EightPlayers.csproj', 'Harness\Harness.csproj') {
    Write-Host "== building $proj"
    & dotnet build (Join-Path $root "src\$proj") -c $Configuration @props
    if ($LASTEXITCODE -ne 0) { throw "build failed: $proj" }
}

$bin = Join-Path $root 'dist\bin'
New-Item -ItemType Directory -Force $bin | Out-Null
Copy-Item (Join-Path $root "src\Patcher\bin\$Configuration\net472\OC2.EightPlayers.Patcher.dll") $bin -Force
Copy-Item (Join-Path $root "src\Plugin\bin\$Configuration\net472\OC2.EightPlayers.dll") $bin -Force

Write-Host '== offline patch check against your Assembly-CSharp.dll'
$tmp = Join-Path $env:TEMP ('oc2-8p-check-' + [guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Force "$tmp\BepInEx" | Out-Null      # the patcher writes diagnostics to .\BepInEx\8P-*.txt
Push-Location $tmp
try {
    $harness = Join-Path $root "src\Harness\bin\$Configuration\net8.0\Harness.exe"
    $out = & $harness (Join-Path $GameDir 'Overcooked2_Data\Managed\Assembly-CSharp.dll') (Join-Path $tmp 'patched.dll') 2>&1
    $out | ForEach-Object { Write-Host $_ }
    if ($out -match 'PATCH FAILURE' -or $out -notmatch 'reloaded OK') { throw 'Offline patch check failed. This game build is not supported by this version of the mod.' }
} finally { Pop-Location; Remove-Item $tmp -Recurse -Force -ErrorAction SilentlyContinue }

Write-Host "`nBuild OK -> $bin"
