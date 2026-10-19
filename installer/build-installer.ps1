<#
.SYNOPSIS
    Publishes PCGet (unpackaged, self-contained) and compiles the Inno Setup installer.

.EXAMPLE
    .\installer\build-installer.ps1
    .\installer\build-installer.ps1 -Arch x64,arm64 -Version 1.0.1.0
#>
[CmdletBinding()]
param(
    [ValidateSet('x64', 'arm64')]
    [string[]] $Arch = @('x64'),

    # Defaults to the Version in PCGet.Package\Package.appxmanifest so MSIX and installer stay in step.
    [string] $Version,

    [string] $Configuration = 'Release',

    # Path to ISCC.exe; found automatically when Inno Setup 6 is installed normally.
    [string] $Iscc
)

$ErrorActionPreference = 'Stop'

$installerDir = $PSScriptRoot
$repoRoot = Split-Path $installerDir -Parent

if (-not $Version) {
    [xml] $manifest = Get-Content (Join-Path $repoRoot 'PCGet.Package\Package.appxmanifest')
    $Version = $manifest.Package.Identity.Version
}

if (-not $Iscc) {
    $onPath = Get-Command iscc.exe -ErrorAction SilentlyContinue

    $candidates = @(
        $(if ($onPath) { $onPath.Source })
        "${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe"
        "$env:ProgramFiles\Inno Setup 6\ISCC.exe"
        "$env:LOCALAPPDATA\Programs\Inno Setup 6\ISCC.exe"
    ) | Where-Object { $_ -and (Test-Path $_) }

    $Iscc = $candidates | Select-Object -First 1

    if (-not $Iscc) {
        throw 'ISCC.exe not found. Install Inno Setup 6 (winget install JRSoftware.InnoSetup) or pass -Iscc.'
    }
}

function Invoke-Publish([string] $project, [string] $rid, [string] $output, [string[]] $extra = @()) {
    Write-Host "Publishing $(Split-Path $project -Leaf) ($rid)..." -ForegroundColor Cyan

    & dotnet publish $project `
        -c $Configuration `
        -r $rid `
        --self-contained true `
        -p:Version=$Version `
        -o $output `
        @extra

    if ($LASTEXITCODE -ne 0) { throw "dotnet publish failed for $project ($rid)." }
}

foreach ($a in $Arch) {
    $rid = "win-$a"
    $platform = if ($a -eq 'arm64') { 'ARM64' } else { 'x64' }
    $stage = Join-Path $installerDir "build\$a"

    if (Test-Path $stage) { Remove-Item $stage -Recurse -Force }

    Invoke-Publish (Join-Path $repoRoot 'PCGet\PCGet.csproj') $rid (Join-Path $stage 'PCGet') @("-p:Platform=$platform", '-p:PCGetUnpackaged=true')
    Invoke-Publish (Join-Path $repoRoot 'PCGet.Elevated\PCGet.Elevated.csproj') $rid (Join-Path $stage 'PCGet.Elevated')
    Invoke-Publish (Join-Path $repoRoot 'PCGet.Updater\PCGet.Updater.csproj') $rid (Join-Path $stage 'PCGet.Updater')

    Write-Host "Compiling installer ($a, v$Version)..." -ForegroundColor Cyan

    & $Iscc "/DAppVersion=$Version" "/DArch=$a" "/DSourceDir=$stage" (Join-Path $installerDir 'PCGet.iss')

    if ($LASTEXITCODE -ne 0) { throw "ISCC failed for $a." }
}

Write-Host "Done. Installers are in $(Join-Path $installerDir 'Output')" -ForegroundColor Green
