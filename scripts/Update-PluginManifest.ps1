#Requires -Version 5.1

[CmdletBinding()]
param(
    [string]$ManifestPath = 'manifest.json',
    [Parameter(Mandatory)]
    [string[]]$EntryPath,
    [string]$PluginGuid = '33fc255a-be9b-11ef-993c-272469e0c801'
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function Assert-FourPartVersion {
    param(
        [Parameter(Mandatory)]
        [string]$Value,
        [Parameter(Mandatory)]
        [string]$Description
    )

    if ($Value -notmatch '^\d+\.\d+\.\d+\.\d+$') {
        throw "$Description must be a four-part numeric version, but was '$Value'."
    }

    return [System.Version]::Parse($Value)
}

if (-not (Test-Path -LiteralPath $ManifestPath -PathType Leaf)) {
    throw "Plugin catalog manifest was not found: $ManifestPath"
}

$resolvedManifestPath = (Resolve-Path -LiteralPath $ManifestPath).Path
$parsedCatalog = [System.IO.File]::ReadAllText($resolvedManifestPath) | ConvertFrom-Json
$catalog = @($parsedCatalog | ForEach-Object { $_ })
$plugin = $catalog | Where-Object { [string]$_.guid -eq $PluginGuid } | Select-Object -First 1
if ($null -eq $plugin) {
    throw "Plugin '$PluginGuid' is not present in '$resolvedManifestPath'."
}
if ($plugin.PSObject.Properties.Name -notcontains 'versions') {
    throw "Plugin '$PluginGuid' has no versions array."
}

$newEntries = @()
foreach ($path in $EntryPath) {
    if (-not (Test-Path -LiteralPath $path -PathType Leaf)) {
        throw "Generated manifest entry was not found: $path"
    }

    $entry = [System.IO.File]::ReadAllText((Resolve-Path -LiteralPath $path).Path) | ConvertFrom-Json
    foreach ($propertyName in @('version', 'changelog', 'targetAbi', 'sourceUrl', 'checksum', 'timestamp')) {
        if ($entry.PSObject.Properties.Name -notcontains $propertyName) {
            throw "Generated manifest entry '$path' is missing '$propertyName'."
        }
    }

    $null = Assert-FourPartVersion -Value ([string]$entry.version) -Description "$path version"
    $null = Assert-FourPartVersion -Value ([string]$entry.targetAbi) -Description "$path targetAbi"
    if ([string]$entry.checksum -notmatch '^[A-Fa-f0-9]{32}$') {
        throw "Generated manifest entry '$path' has an invalid MD5 checksum."
    }

    $sourceUri = $null
    if (-not [System.Uri]::TryCreate([string]$entry.sourceUrl, [System.UriKind]::Absolute, [ref]$sourceUri) -or
        $sourceUri.Scheme -cne 'https' -or
        $sourceUri.Host -cne 'github.com') {
        throw "Generated manifest entry '$path' has an invalid GitHub release URL."
    }

    $parsedTimestamp = [System.DateTimeOffset]::MinValue
    if (-not [System.DateTimeOffset]::TryParse([string]$entry.timestamp, [ref]$parsedTimestamp)) {
        throw "Generated manifest entry '$path' has an invalid timestamp."
    }

    $newEntries += $entry
}

$duplicateNewVersion = $newEntries |
    Group-Object { [string]$_.version } |
    Where-Object Count -gt 1 |
    Select-Object -First 1
if ($null -ne $duplicateNewVersion) {
    throw "Generated entries contain duplicate plugin version '$($duplicateNewVersion.Name)'."
}

$newVersions = @($newEntries | ForEach-Object { [string]$_.version })
$mergedVersions = @(
    $newEntries
    @($plugin.versions) | Where-Object { [string]$_.version -notin $newVersions }
) | Sort-Object -Property @{ Expression = { [System.Version]::Parse([string]$_.version) }; Descending = $true }

$plugin.versions = @($mergedVersions)
$json = ConvertTo-Json -InputObject $catalog -Depth 100
[System.IO.File]::WriteAllText(
    $resolvedManifestPath,
    "$json`n",
    [System.Text.UTF8Encoding]::new($false)
)

Write-Host "Updated $resolvedManifestPath with $($newEntries.Count) generated release entries."
