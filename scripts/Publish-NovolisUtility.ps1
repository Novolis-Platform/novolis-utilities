#Requires -Version 7.0
[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [string]$Utility,
    [string]$RepoRoot = (Split-Path $PSScriptRoot -Parent),
    [string]$PackageVersion
)

$ErrorActionPreference = 'Stop'
$RepoRoot = (Resolve-Path $RepoRoot).Path
$manifestPath = Join-Path $RepoRoot 'build\utilities.json'
$manifest = Get-Content -LiteralPath $manifestPath -Raw | ConvertFrom-Json
$item = @($manifest.utilities) | Where-Object {
    $_.key -ieq $Utility
} | Select-Object -First 1

if (-not $item) {
    throw "Unknown utility '$Utility'. See $manifestPath."
}
if (@($item.ship) -notcontains 'windows-zip') {
    throw "Utility '$Utility' does not declare the windows-zip channel."
}

if (-not $PackageVersion) {
    $versionProps = Join-Path $RepoRoot 'build\version.props'
    if (Test-Path $versionProps) {
        [xml]$versionXml = Get-Content -LiteralPath $versionProps -Raw
        $platformVersion = [string]$versionXml.Project.PropertyGroup.NovolisPlatformVersion
    }
    if (-not $platformVersion) {
        $platformVersion = '2026.1.1'
    }
    $build = if ($env:GITHUB_RUN_NUMBER) { $env:GITHUB_RUN_NUMBER } else { '0' }
    $PackageVersion = "$platformVersion.$build"
}

$projectPath = Join-Path $RepoRoot ($item.project -replace '/', '\')
if (-not (Test-Path $projectPath)) {
    throw "Utility project does not exist: $projectPath"
}

$artifactRoot = Join-Path $RepoRoot "artifacts\$($item.key)\$PackageVersion"
$publishRoot = Join-Path $artifactRoot 'publish'
$zipPath = Join-Path $artifactRoot "$($item.key)-$PackageVersion-win-x64.zip"
$checksumsPath = Join-Path $artifactRoot 'SHA256SUMS.txt'

if (Test-Path $artifactRoot) {
    Remove-Item -LiteralPath $artifactRoot -Recurse -Force
}
New-Item -ItemType Directory -Path $publishRoot -Force | Out-Null

$configPath = Join-Path $RepoRoot 'nuget.config'
& dotnet publish $projectPath `
    --configuration Release `
    --runtime win-x64 `
    --self-contained false `
    --output $publishRoot `
    --configfile $configPath
if ($LASTEXITCODE -ne 0) {
    throw "dotnet publish failed for $($item.key)."
}

Compress-Archive -Path (Join-Path $publishRoot '*') -DestinationPath $zipPath -CompressionLevel Optimal
$hash = (Get-FileHash -LiteralPath $zipPath -Algorithm SHA256).Hash.ToLowerInvariant()
Set-Content -LiteralPath $checksumsPath -Value "$hash  $([IO.Path]::GetFileName($zipPath))" -Encoding utf8NoBOM

Remove-Item -LiteralPath $publishRoot -Recurse -Force

[PSCustomObject]@{
    Utility = $item.key
    Version = $PackageVersion
    ZipPath = $zipPath
    ChecksumsPath = $checksumsPath
}
