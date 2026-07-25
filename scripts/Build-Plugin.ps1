#Requires -Version 5.1

[CmdletBinding()]
param(
  [string]$OutputDirectory = 'release'
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$ProgressPreference = 'SilentlyContinue'

function Invoke-DotNet {
  param(
    [Parameter(Mandatory)]
    [string[]]$Arguments,
    [Parameter(Mandatory)]
    [string]$Description
  )

  & dotnet @Arguments
  if ($LASTEXITCODE -ne 0)
  {
    throw "$Description failed with exit code $LASTEXITCODE."
  }
}

if ($null -eq (Get-Command dotnet -ErrorAction SilentlyContinue))
{
  throw 'The .NET SDK was not found. Install the .NET 8 and .NET 9 SDKs, then try again.'
}

$sdkVersions = @(& dotnet --list-sdks)
if ($LASTEXITCODE -ne 0)
{
  throw 'Unable to list the installed .NET SDKs.'
}

if (-not ($sdkVersions -match '^8\.'))
{
  throw 'The .NET 8 SDK is required to build and test the Jellyfin 10.10 package.'
}

if (-not ($sdkVersions -match '^9\.'))
{
  throw 'The .NET 9 SDK is required to build and test the Jellyfin 10.11 package.'
}

$repositoryRoot = Split-Path -Parent $PSScriptRoot
Push-Location $repositoryRoot
try
{
  Write-Host 'Restoring, building, and testing both Jellyfin ABI targets...'
  Invoke-DotNet `
    -Arguments @('restore', 'Jellyfin.Plugin.AudioTagger.sln') `
    -Description 'Solution restore'
  Invoke-DotNet `
    -Arguments @(
      'build',
      'Jellyfin.Plugin.AudioTagger.sln',
      '--configuration',
      'Release',
      '--no-restore'
    ) `
    -Description 'Solution build'
  Invoke-DotNet `
    -Arguments @(
      'test',
      'Jellyfin.Plugin.AudioTagger.sln',
      '--configuration',
      'Release',
      '--no-build',
      '--no-restore'
    ) `
    -Description 'Test suite'

  $versionOutput = @(
    & dotnet msbuild Jellyfin.Plugin.AudioTagger.csproj `
      -nologo `
      -getProperty:Version `
      -property:TargetFramework=net9.0
  )
  if ($LASTEXITCODE -ne 0)
  {
    throw 'Unable to read the net9.0 plugin version.'
  }

  $releaseVersion = (
    $versionOutput |
      Where-Object { -not [string]::IsNullOrWhiteSpace($_) } |
      Select-Object -Last 1
  )
  if ([string]::IsNullOrWhiteSpace($releaseVersion))
  {
    throw 'The net9.0 plugin version is empty.'
  }

  $releaseVersion = $releaseVersion.Trim()
  $releaseOutputDirectory = Join-Path $OutputDirectory "v$releaseVersion"
  $packager = Join-Path $PSScriptRoot 'New-PluginPackage.ps1'
  $targets = @(
    [pscustomobject]@{
      Framework = 'net8.0'
      DllPath = 'bin/Release/net8.0/Jellyfin.Plugin.AudioTagger.dll'
    }
    [pscustomobject]@{
      Framework = 'net9.0'
      DllPath = 'bin/Release/net9.0/Jellyfin.Plugin.AudioTagger.dll'
    }
  )

  Write-Host 'Creating validated Jellyfin 10.10 and 10.11 packages...'
  foreach ($target in $targets)
  {
    & $packager `
      -TargetFramework $target.Framework `
      -DllPath $target.DllPath `
      -OutputDirectory $releaseOutputDirectory `
      -Repository 'sudo-kraken/jellyfin-plugin-audiotagger' `
      -ReleaseTag "v$releaseVersion"
  }

  Write-Host "Packages and checksums are available in $releaseOutputDirectory."
}
finally
{
  Pop-Location
}
