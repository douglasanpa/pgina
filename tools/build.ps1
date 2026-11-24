<#
.SYNOPSIS
Builds pGina (Win32 and x64) and every plugin solution.

.DESCRIPTION
Finds MSBuild with vswhere. The native projects use the v145 toolset (Visual Studio 2026);
when it isn't installed, the newest installed one is used instead (v143 for Visual Studio 2022).
Output goes to pGina\src\bin and Plugins\bin.
#>
param(
    [string]$Configuration = "Release",
    [switch]$Clean
)
$ErrorActionPreference = "Stop"
$repo = Split-Path $PSScriptRoot -Parent

$vswhere = "${env:ProgramFiles(x86)}\Microsoft Visual Studio\Installer\vswhere.exe"
$vs = & $vswhere -latest -prerelease -products * -requires Microsoft.Component.MSBuild Microsoft.VisualStudio.Component.VC.Tools.x86.x64 -property installationPath
if (-not $vs) { throw "Visual Studio with the C++ tools not found" }
$msbuild = Join-Path $vs "MSBuild\Current\Bin\MSBuild.exe"

$toolsets = @(Get-ChildItem "$vs\MSBuild\Microsoft\VC\*\Platforms\x64\PlatformToolsets\*" -Directory -ErrorAction SilentlyContinue |
    Where-Object Name -match '^v1\d\d$' | Select-Object -ExpandProperty Name | Sort-Object -Unique)
if (-not $toolsets) { throw "No C++ platform toolset found under $vs" }
$extra = @()
if ($toolsets -notcontains "v145") {
    $toolset = $toolsets | Sort-Object -Descending | Select-Object -First 1
    Write-Host "v145 toolset not installed, using $toolset"
    $extra += "-p:PlatformToolset=$toolset"
}

if ($Clean) {
    foreach ($d in "$repo\pGina\src\bin", "$repo\pGina\src\build", "$repo\Plugins\bin") {
        if (Test-Path $d) { Remove-Item -Recurse -Force $d }
    }
}

$failed = @()
function Build([string]$sln, [string]$platform) {
    $name = (Split-Path $sln -Leaf) + $(if ($platform) { " ($platform)" } else { "" })
    Write-Host "::group::$name"
    $a = @($sln, "-restore", "-m", "-nologo", "-v:m", "-p:Configuration=$Configuration") + $extra
    if ($platform) { $a += "-p:Platform=$platform" }
    & $msbuild @a
    if ($LASTEXITCODE -ne 0) { $script:failed += $name }
    Write-Host "::endgroup::"
}

Build "$repo\pGina\src\pGina-3.x-vs2010.sln" "Win32"
Build "$repo\pGina\src\pGina-3.x-vs2010.sln" "x64"
Get-ChildItem "$repo\Plugins" -Recurse -Filter *.sln | Sort-Object FullName | ForEach-Object { Build $_.FullName $null }

if ($failed) {
    Write-Host "Failed: $($failed -join ', ')"
    exit 1
}
Write-Host "All builds succeeded"
