param(
    [string]$Version,
    [string]$RepoRoot
)

$ErrorActionPreference = "Stop"

if ([string]::IsNullOrWhiteSpace($RepoRoot)) {
    $RepoRoot = Split-Path $PSScriptRoot -Parent
}

function Get-ThreePartVersion([string]$value) {
    if ([string]::IsNullOrWhiteSpace($value)) {
        return $null
    }

    if ($value -match "^(\d+\.\d+\.\d+)") {
        return $Matches[1]
    }

    return $null
}

function Get-VersionFromFile([string]$path, [string]$pattern) {
    if (-not (Test-Path $path)) {
        return $null
    }

    foreach ($line in Get-Content $path) {
        if ($line -match $pattern) {
            return $Matches[1]
        }
    }

    return $null
}

$pluginVersion = Get-ThreePartVersion $Version
$versionSource = "parameter"

if (-not $pluginVersion) {
    $pluginVersion = Get-ThreePartVersion $env:flowVersion
    $versionSource = "flowVersion environment variable"
}

if (-not $pluginVersion) {
    $pluginVersion = Get-VersionFromFile `
        (Join-Path $RepoRoot ".github\workflows\dotnet.yml") `
        "^\s*VersionPrefix:\s*(\d+\.\d+\.\d+)"
    $versionSource = "VersionPrefix in dotnet.yml"
}

if (-not $pluginVersion) {
    $pluginVersion = Get-VersionFromFile `
        (Join-Path $RepoRoot "appveyor.yml") `
        "version:\s*'?(\d+\.\d+\.\d+)\."
    $versionSource = "appveyor.yml"
}

if (-not $pluginVersion) {
    throw "Unable to resolve a production plugin version. Pass -Version, set flowVersion, or provide VersionPrefix / appveyor.yml."
}

if ($pluginVersion -eq "1.0.0") {
    throw "Refusing to stamp bundled plugins as 1.0.0, which is the in-repo development placeholder. Set a production version first."
}

$jsonFiles = @(Get-ChildItem -Path (Join-Path $RepoRoot "Plugins\*\plugin.json") -ErrorAction SilentlyContinue)
if ($jsonFiles.Count -eq 0) {
    throw "No plugin.json files found under $(Join-Path $RepoRoot 'Plugins')."
}

Write-Host "Stamping $($jsonFiles.Count) bundled plugin(s) to $pluginVersion ($versionSource)"

$utf8NoBom = New-Object System.Text.UTF8Encoding $false
foreach ($file in $jsonFiles) {
    $content = [System.IO.File]::ReadAllText($file.FullName)
    $updated = [regex]::Replace($content, '"Version"\s*:\s*".*?"', "`"Version`": `"$pluginVersion`"")
    $parsed = $updated | ConvertFrom-Json
    if ($parsed.Version -ne $pluginVersion) {
        throw "Failed to stamp version on $($file.FullName)"
    }

    [System.IO.File]::WriteAllText($file.FullName, $updated, $utf8NoBom)
    Write-Host "Updated $($parsed.Name) to $($parsed.Version)"
}
