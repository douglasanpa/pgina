<#
.SYNOPSIS
Builds pGina and its installer: Installer\Output\pGinaSetup-<version>.exe

.PARAMETER Version
Installer version, e.g. 3.10.0. Defaults to the assembly version.
#>
param(
    [string]$Version,
    [switch]$SkipBuild
)
$ErrorActionPreference = "Stop"
$repo = Split-Path $PSScriptRoot -Parent

if (-not $SkipBuild) {
    & "$repo\tools\build.ps1" -Configuration Release
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
}
if (-not $Version) {
    $Version = (Get-Item "$repo\pGina\src\bin\pGina.Configuration.exe").VersionInfo.FileVersion
}

$iscc = @(
    (Get-Command ISCC.exe -ErrorAction SilentlyContinue).Source,
    "${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe",
    "$env:ProgramFiles\Inno Setup 6\ISCC.exe",
    "$env:LOCALAPPDATA\Programs\Inno Setup 6\ISCC.exe"
) | Where-Object { $_ -and (Test-Path $_) } | Select-Object -First 1
if (-not $iscc) { throw "Inno Setup 6 not found (https://jrsoftware.org/isdl.php)" }

& $iscc "/DMyAppVersion=$Version" "$PSScriptRoot\installer.iss"
exit $LASTEXITCODE
