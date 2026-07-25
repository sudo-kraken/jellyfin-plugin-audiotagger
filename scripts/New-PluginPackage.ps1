#Requires -Version 5.1

[CmdletBinding()]
param(
    [string]$ProjectPath = 'Jellyfin.Plugin.AudioTagger.csproj',
    [string]$TargetFramework,
    [string]$DllPath,
    [string]$OutputDirectory = 'release',
    [ValidateSet('Debug', 'Release')]
    [string]$Configuration = 'Release',
    [string]$Repository = $env:GITHUB_REPOSITORY,
    [string]$ReleaseTag = $env:GITHUB_REF_NAME,
    [string]$Changelog,
    [string]$Timestamp,
    [string]$PluginVersion,
    [string]$TargetAbi,
    [string]$JellyfinVersion,
    [string]$HostingAbstractionsVersion,
    [string]$NuGetLockPath,
    [switch]$GenerateSbom,
    [string]$ValidatePackage,
    [string]$SbomPath
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$ProgressPreference = 'SilentlyContinue'

$PluginId = '33fc255a-be9b-11ef-993c-272469e0c801'
$PluginName = 'Audio Tagger'
$PluginAssemblyName = 'Jellyfin.Plugin.AudioTagger'
$PluginAssemblyFileName = "$PluginAssemblyName.dll"
$PluginCategory = 'Metadata'
$PluginOwner = 'sudo-kraken'
$PluginDescription = 'Automatically adds audio format tags to movies based on their audio streams (5.1, 7.1, Atmos, DTS, etc.)'
$PluginOverview = 'Automatic audio tagging plugin for Jellyfin that analyzes movie audio streams and adds descriptive tags based on channel layout, codec, and audio quality.'

Add-Type -AssemblyName System.IO.Compression
Add-Type -AssemblyName System.IO.Compression.FileSystem

function Resolve-ExistingFile {
    param(
        [Parameter(Mandatory)]
        [string]$Path,
        [Parameter(Mandatory)]
        [string]$Description
    )

    if (-not (Test-Path -LiteralPath $Path -PathType Leaf)) {
        throw "$Description was not found: $Path"
    }

    return (Resolve-Path -LiteralPath $Path).Path
}

function Resolve-Directory {
    param(
        [Parameter(Mandatory)]
        [string]$Path
    )

    $null = New-Item -ItemType Directory -Path $Path -Force
    return (Resolve-Path -LiteralPath $Path).Path
}

function Write-Utf8Json {
    param(
        [Parameter(Mandatory)]
        [object]$Value,
        [Parameter(Mandatory)]
        [string]$Path
    )

    $json = $Value | ConvertTo-Json -Depth 100
    [System.IO.File]::WriteAllText(
        $Path,
        "$json`n",
        [System.Text.UTF8Encoding]::new($false)
    )
}

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

    try {
        return [System.Version]::Parse($Value)
    }
    catch {
        throw "$Description is not a valid version: '$Value'."
    }
}

function Get-MsBuildProperty {
    param(
        [Parameter(Mandatory)]
        [string]$ResolvedProjectPath,
        [Parameter(Mandatory)]
        [string]$PropertyName,
        [Parameter(Mandatory)]
        [string]$Framework
    )

    $arguments = @(
        'msbuild'
        $ResolvedProjectPath
        '-nologo'
        "-getProperty:$PropertyName"
        "-property:TargetFramework=$Framework"
    )
    $propertyOutput = @(& dotnet @arguments)
    if ($LASTEXITCODE -ne 0) {
        throw "Unable to read MSBuild property '$PropertyName' for '$Framework'."
    }

    $value = ($propertyOutput | Where-Object { -not [string]::IsNullOrWhiteSpace($_) } | Select-Object -Last 1).Trim()
    if ([string]::IsNullOrWhiteSpace($value)) {
        throw "MSBuild property '$PropertyName' is empty for '$Framework'."
    }

    return $value
}

function Get-Checksum {
    param(
        [Parameter(Mandatory)]
        [string]$Path,
        [Parameter(Mandatory)]
        [ValidateSet('MD5', 'SHA256', 'SHA512')]
        [string]$Algorithm
    )

    return (Get-FileHash -LiteralPath $Path -Algorithm $Algorithm).Hash.ToUpperInvariant()
}

function Write-ChecksumFile {
    param(
        [Parameter(Mandatory)]
        [string]$Hash,
        [Parameter(Mandatory)]
        [string]$FileName,
        [Parameter(Mandatory)]
        [string]$Path
    )

    [System.IO.File]::WriteAllText(
        $Path,
        "$Hash  $FileName`n",
        [System.Text.UTF8Encoding]::new($false)
    )
}

function Get-ChecksumFromFile {
    param(
        [Parameter(Mandatory)]
        [string]$Path,
        [Parameter(Mandatory)]
        [int]$Length
    )

    if (-not (Test-Path -LiteralPath $Path -PathType Leaf)) {
        throw "Checksum file is missing: $Path"
    }

    $line = ([System.IO.File]::ReadAllText((Resolve-Path -LiteralPath $Path).Path)).Trim()
    if ($line -notmatch "^(?<hash>[A-Fa-f0-9]{$Length})\s+\*?.+$") {
        throw "Checksum file has an invalid format: $Path"
    }

    return $Matches.hash.ToUpperInvariant()
}

function Get-PropertyValue {
    param(
        [Parameter(Mandatory)]
        [object]$Object,
        [Parameter(Mandatory)]
        [string]$Name
    )

    if ($Object.PSObject.Properties.Name -notcontains $Name) {
        throw "JSON property '$Name' is missing."
    }

    return $Object.$Name
}

function Test-ManagedPluginAssembly {
    param(
        [Parameter(Mandatory)]
        [string]$Path,
        [Parameter(Mandatory)]
        [string]$ExpectedVersion
    )

    try {
        $assemblyName = [System.Reflection.AssemblyName]::GetAssemblyName($Path)
    }
    catch {
        throw "The packaged plugin DLL is not a readable .NET assembly: $($_.Exception.Message)"
    }

    if ($assemblyName.Name -cne $PluginAssemblyName) {
        throw "Expected assembly '$PluginAssemblyName', but found '$($assemblyName.Name)'."
    }

    if ($assemblyName.Version.ToString() -ne $ExpectedVersion) {
        throw "Assembly version '$($assemblyName.Version)' does not match plugin version '$ExpectedVersion'."
    }
}

function Read-PluginPackage {
    param(
        [Parameter(Mandatory)]
        [string]$Path
    )

    $resolvedPackagePath = Resolve-ExistingFile -Path $Path -Description 'Plugin package'
    $temporaryDirectory = Join-Path ([System.IO.Path]::GetTempPath()) "audiotagger-validate-$([guid]::NewGuid().ToString('N'))"
    $null = New-Item -ItemType Directory -Path $temporaryDirectory

    try {
        $archive = [System.IO.Compression.ZipFile]::OpenRead($resolvedPackagePath)
        try {
            $entries = @($archive.Entries)
            $entryNames = @($entries | ForEach-Object { $_.FullName })
            $expectedEntryNames = @($PluginAssemblyFileName, 'meta.json')

            if ($entries.Count -ne $expectedEntryNames.Count) {
                throw "Plugin ZIP must contain exactly $($expectedEntryNames.Count) files, but contains $($entries.Count): $($entryNames -join ', ')."
            }

            foreach ($entry in $entries) {
                if ($entry.FullName -notin $expectedEntryNames -or
                    $entry.FullName.Contains('/') -or
                    $entry.FullName.Contains('\') -or
                    [string]::IsNullOrWhiteSpace($entry.Name)) {
                    throw "Plugin ZIP contains an unexpected or non-flat entry: '$($entry.FullName)'."
                }
            }

            $dllEntry = $entries | Where-Object { $_.FullName -ceq $PluginAssemblyFileName } | Select-Object -First 1
            $manifestEntry = $entries | Where-Object { $_.FullName -ceq 'meta.json' } | Select-Object -First 1
            if ($null -eq $dllEntry -or $null -eq $manifestEntry) {
                throw 'Plugin ZIP does not contain both the plugin DLL and meta.json.'
            }

            if ($dllEntry.Length -le 0 -or $dllEntry.Length -gt 50MB) {
                throw "Plugin DLL has an unexpected uncompressed size: $($dllEntry.Length) bytes."
            }

            if ($manifestEntry.Length -le 0 -or $manifestEntry.Length -gt 128KB) {
                throw "meta.json has an unexpected uncompressed size: $($manifestEntry.Length) bytes."
            }

            $manifestReader = [System.IO.StreamReader]::new($manifestEntry.Open(), [System.Text.Encoding]::UTF8, $true)
            try {
                $manifest = $manifestReader.ReadToEnd() | ConvertFrom-Json
            }
            finally {
                $manifestReader.Dispose()
            }

            $requiredManifestProperties = @(
                'category',
                'changelog',
                'description',
                'guid',
                'name',
                'overview',
                'owner',
                'targetAbi',
                'timestamp',
                'version',
                'status',
                'autoUpdate',
                'assemblies'
            )
            foreach ($propertyName in $requiredManifestProperties) {
                $null = Get-PropertyValue -Object $manifest -Name $propertyName
            }

            if ([string]$manifest.guid -ne $PluginId) {
                throw "meta.json GUID '$($manifest.guid)' does not match '$PluginId'."
            }

            if ([string]$manifest.name -cne $PluginName -or
                [string]$manifest.category -cne $PluginCategory -or
                [string]$manifest.owner -cne $PluginOwner) {
                throw 'meta.json plugin identity fields are invalid.'
            }

            $null = Assert-FourPartVersion -Value ([string]$manifest.version) -Description 'meta.json version'
            $null = Assert-FourPartVersion -Value ([string]$manifest.targetAbi) -Description 'meta.json targetAbi'

            if ([int]$manifest.status -ne 0) {
                throw "meta.json status must be the numeric value 0, but was '$($manifest.status)'."
            }

            if ([bool]$manifest.autoUpdate -ne $true) {
                throw 'meta.json autoUpdate must be true.'
            }

            $assemblies = @($manifest.assemblies)
            if ($assemblies.Count -ne 1 -or [string]$assemblies[0] -cne $PluginAssemblyFileName) {
                throw "meta.json assemblies must whitelist only '$PluginAssemblyFileName'."
            }

            $parsedTimestamp = [System.DateTimeOffset]::MinValue
            if (-not [System.DateTimeOffset]::TryParse(
                    [string]$manifest.timestamp,
                    [System.Globalization.CultureInfo]::InvariantCulture,
                    [System.Globalization.DateTimeStyles]::AssumeUniversal,
                    [ref]$parsedTimestamp)) {
                throw "meta.json timestamp is invalid: '$($manifest.timestamp)'."
            }

            $extractedDllPath = Join-Path $temporaryDirectory $PluginAssemblyFileName
            [System.IO.Compression.ZipFileExtensions]::ExtractToFile($dllEntry, $extractedDllPath, $false)
            Test-ManagedPluginAssembly -Path $extractedDllPath -ExpectedVersion ([string]$manifest.version)

            return [pscustomobject]@{
                PackagePath = $resolvedPackagePath
                Manifest = $manifest
                ExtractedDllPath = $extractedDllPath
                TemporaryDirectory = $temporaryDirectory
            }
        }
        finally {
            $archive.Dispose()
        }
    }
    catch {
        if (Test-Path -LiteralPath $temporaryDirectory) {
            Remove-Item -LiteralPath $temporaryDirectory -Recurse -Force
        }

        throw
    }
}

function Test-PackageChecksumsAndEntry {
    param(
        [Parameter(Mandatory)]
        [object]$PackageInfo
    )

    $packagePath = $PackageInfo.PackagePath
    $packageFileName = [System.IO.Path]::GetFileName($packagePath)
    $packageStem = [System.IO.Path]::GetFileNameWithoutExtension($packagePath)
    $packageDirectory = [System.IO.Path]::GetDirectoryName($packagePath)

    $algorithms = @(
        [pscustomobject]@{ Name = 'MD5'; Length = 32; Suffix = 'md5' }
        [pscustomobject]@{ Name = 'SHA256'; Length = 64; Suffix = 'sha256' }
        [pscustomobject]@{ Name = 'SHA512'; Length = 128; Suffix = 'sha512' }
    )

    $actualHashes = @{}
    foreach ($algorithm in $algorithms) {
        $checksumPath = "$packagePath.$($algorithm.Suffix)"
        $recordedHash = Get-ChecksumFromFile -Path $checksumPath -Length $algorithm.Length
        $actualHash = Get-Checksum -Path $packagePath -Algorithm $algorithm.Name
        if ($recordedHash -cne $actualHash) {
            throw "$($algorithm.Name) checksum does not match for '$packageFileName'."
        }

        $actualHashes[$algorithm.Name] = $actualHash
    }

    $manifestEntryPath = Join-Path $packageDirectory "$packageStem.manifest-entry.json"
    $resolvedManifestEntryPath = Resolve-ExistingFile -Path $manifestEntryPath -Description 'Generated catalog manifest entry'
    $catalogEntry = [System.IO.File]::ReadAllText($resolvedManifestEntryPath) | ConvertFrom-Json

    foreach ($propertyName in @('version', 'changelog', 'targetAbi', 'sourceUrl', 'checksum', 'timestamp')) {
        $null = Get-PropertyValue -Object $catalogEntry -Name $propertyName
    }

    if ([string]$catalogEntry.version -ne [string]$PackageInfo.Manifest.version -or
        [string]$catalogEntry.targetAbi -ne [string]$PackageInfo.Manifest.targetAbi -or
        [string]$catalogEntry.timestamp -ne [string]$PackageInfo.Manifest.timestamp) {
        throw 'Catalog manifest entry does not match the package meta.json.'
    }

    if ([string]$catalogEntry.checksum -cne $actualHashes.MD5) {
        throw 'Catalog manifest entry does not contain the package MD5 checksum.'
    }

    $sourceUri = $null
    if (-not [System.Uri]::TryCreate([string]$catalogEntry.sourceUrl, [System.UriKind]::Absolute, [ref]$sourceUri) -or
        $sourceUri.Scheme -cne 'https' -or
        $sourceUri.Host -cne 'github.com' -or
        [System.IO.Path]::GetFileName($sourceUri.AbsolutePath) -cne $packageFileName) {
        throw "Catalog manifest entry has an invalid sourceUrl: '$($catalogEntry.sourceUrl)'."
    }

    return $actualHashes
}

function Get-SbomProperty {
    param(
        [Parameter(Mandatory)]
        [object]$Component,
        [Parameter(Mandatory)]
        [string]$Name
    )

    $property = @($Component.properties) | Where-Object { [string]$_.name -ceq $Name } | Select-Object -First 1
    if ($null -eq $property -or [string]::IsNullOrWhiteSpace([string]$property.value)) {
        throw "SBOM component property '$Name' is missing."
    }

    return [string]$property.value
}

function Test-PluginSbom {
    param(
        [Parameter(Mandatory)]
        [string]$Path,
        [Parameter(Mandatory)]
        [object]$PackageInfo
    )

    $resolvedSbomPath = Resolve-ExistingFile -Path $Path -Description 'Plugin SBOM'
    $sbom = [System.IO.File]::ReadAllText($resolvedSbomPath) | ConvertFrom-Json

    if ([string](Get-PropertyValue -Object $sbom -Name 'bomFormat') -cne 'CycloneDX') {
        throw "SBOM '$resolvedSbomPath' is not CycloneDX JSON."
    }

    $metadata = Get-PropertyValue -Object $sbom -Name 'metadata'
    $rootComponent = Get-PropertyValue -Object $metadata -Name 'component'
    if ([string]$rootComponent.name -cne $PluginName -or
        [string]$rootComponent.version -ne [string]$PackageInfo.Manifest.version) {
        throw 'SBOM root component does not match the packaged plugin.'
    }

    $sbomTargetAbi = Get-SbomProperty -Component $rootComponent -Name 'jellyfin:targetAbi'
    $sbomFramework = Get-SbomProperty -Component $rootComponent -Name 'dotnet:targetFramework'
    $sbomJellyfinVersion = Get-SbomProperty -Component $rootComponent -Name 'jellyfin:packageVersion'
    $sbomHostingVersion = Get-SbomProperty -Component $rootComponent -Name 'dotnet:hostingAbstractionsVersion'
    if ($sbomTargetAbi -ne [string]$PackageInfo.Manifest.targetAbi -or
        $sbomFramework -notmatch '^net\d+\.\d+$') {
        throw 'SBOM target metadata does not match the packaged plugin.'
    }

    $components = @(Get-PropertyValue -Object $sbom -Name 'components')
    if ($components.Count -eq 0) {
        throw 'SBOM has no dependency components.'
    }

    foreach ($dependency in @(
            [pscustomobject]@{ Name = 'Jellyfin.Controller'; Version = $sbomJellyfinVersion }
            [pscustomobject]@{ Name = 'Jellyfin.Model'; Version = $sbomJellyfinVersion }
            [pscustomobject]@{ Name = 'Microsoft.Extensions.Hosting.Abstractions'; Version = $sbomHostingVersion }
        )) {
        $match = $components | Where-Object {
            [string]$_.name -ieq $dependency.Name -and [string]$_.version -eq $dependency.Version
        } | Select-Object -First 1
        if ($null -eq $match) {
            throw "SBOM does not contain '$($dependency.Name)' version '$($dependency.Version)'."
        }
    }

    $dllSha256 = Get-Checksum -Path $PackageInfo.ExtractedDllPath -Algorithm SHA256
    $hash = @($rootComponent.hashes) | Where-Object {
        [string]$_.alg -ceq 'SHA-256' -and [string]$_.content -ieq $dllSha256
    } | Select-Object -First 1
    if ($null -eq $hash) {
        throw 'SBOM root component does not contain the plugin DLL SHA-256 hash.'
    }
}

function New-PluginSbom {
    param(
        [Parameter(Mandatory)]
        [string]$LockFilePath,
        [Parameter(Mandatory)]
        [string]$ResolvedDllPath,
        [Parameter(Mandatory)]
        [string]$ResolvedProjectPath,
        [Parameter(Mandatory)]
        [string]$DestinationPath,
        [Parameter(Mandatory)]
        [string]$Framework,
        [Parameter(Mandatory)]
        [string]$Version,
        [Parameter(Mandatory)]
        [string]$Abi,
        [Parameter(Mandatory)]
        [string]$JellyfinPackageVersion,
        [Parameter(Mandatory)]
        [string]$HostingPackageVersion
    )

    if ($null -eq (Get-Command trivy -ErrorAction SilentlyContinue)) {
        throw 'Trivy is required when -GenerateSbom is specified.'
    }

    $resolvedLockFilePath = Resolve-ExistingFile -Path $LockFilePath -Description 'Framework-specific NuGet lock file'
    $contextDirectory = Join-Path ([System.IO.Path]::GetTempPath()) "audiotagger-sbom-$([guid]::NewGuid().ToString('N'))"
    $null = New-Item -ItemType Directory -Path $contextDirectory -Force
    $temporarySbomPath = Join-Path $contextDirectory 'sbom.cdx.json'

    try {
        Copy-Item -LiteralPath $resolvedLockFilePath -Destination (Join-Path $contextDirectory 'packages.lock.json')
        Copy-Item -LiteralPath $ResolvedDllPath -Destination (Join-Path $contextDirectory $PluginAssemblyFileName)
        Copy-Item -LiteralPath $ResolvedProjectPath -Destination (Join-Path $contextDirectory ([System.IO.Path]::GetFileName($ResolvedProjectPath)))

        & trivy fs --quiet --format cyclonedx --output $temporarySbomPath $contextDirectory
        if ($LASTEXITCODE -ne 0) {
            throw 'Trivy failed to generate the CycloneDX SBOM.'
        }

        $sbom = [System.IO.File]::ReadAllText($temporarySbomPath) | ConvertFrom-Json
        if ([string]$sbom.bomFormat -cne 'CycloneDX') {
            throw 'Trivy did not generate a CycloneDX SBOM.'
        }

        $components = @($sbom.components)
        foreach ($dependency in @(
                [pscustomobject]@{ Name = 'Jellyfin.Controller'; Version = $JellyfinPackageVersion }
                [pscustomobject]@{ Name = 'Jellyfin.Model'; Version = $JellyfinPackageVersion }
                [pscustomobject]@{ Name = 'Microsoft.Extensions.Hosting.Abstractions'; Version = $HostingPackageVersion }
            )) {
            $match = $components | Where-Object {
                [string]$_.name -ieq $dependency.Name -and [string]$_.version -eq $dependency.Version
            } | Select-Object -First 1
            if ($null -eq $match) {
                throw "Generated SBOM does not contain '$($dependency.Name)' version '$($dependency.Version)'."
            }
        }

        $rootReference = "pkg:generic/jellyfin-plugin-audiotagger@$Version"
        $rootComponent = [ordered]@{
            type = 'application'
            'bom-ref' = $rootReference
            group = 'sudo-kraken'
            name = $PluginName
            version = $Version
            hashes = @(
                [ordered]@{
                    alg = 'SHA-256'
                    content = Get-Checksum -Path $ResolvedDllPath -Algorithm SHA256
                }
            )
            properties = @(
                [ordered]@{ name = 'dotnet:targetFramework'; value = $Framework }
                [ordered]@{ name = 'jellyfin:targetAbi'; value = $Abi }
                [ordered]@{ name = 'jellyfin:packageVersion'; value = $JellyfinPackageVersion }
                [ordered]@{ name = 'dotnet:hostingAbstractionsVersion'; value = $HostingPackageVersion }
            )
        }

        if ($null -eq $sbom.metadata) {
            $sbom | Add-Member -NotePropertyName metadata -NotePropertyValue ([pscustomobject]@{}) -Force
        }
        $sbom.metadata | Add-Member -NotePropertyName component -NotePropertyValue $rootComponent -Force

        $dependencyReferences = @(
            $components |
                ForEach-Object { $_.'bom-ref' } |
                Where-Object { -not [string]::IsNullOrWhiteSpace([string]$_) } |
                Sort-Object -Unique
        )
        $rootDependency = [ordered]@{
            ref = $rootReference
            dependsOn = $dependencyReferences
        }
        $existingDependencies = @()
        if ($sbom.PSObject.Properties.Name -contains 'dependencies') {
            $existingDependencies = @($sbom.dependencies | Where-Object { [string]$_.ref -cne $rootReference })
        }
        $sbom | Add-Member -NotePropertyName dependencies -NotePropertyValue @($existingDependencies + $rootDependency) -Force

        Write-Utf8Json -Value $sbom -Path $DestinationPath
    }
    finally {
        if (Test-Path -LiteralPath $contextDirectory) {
            Remove-Item -LiteralPath $contextDirectory -Recurse -Force
        }
    }
}

if (-not [string]::IsNullOrWhiteSpace($ValidatePackage)) {
    $packageInfo = Read-PluginPackage -Path $ValidatePackage
    try {
        $null = Test-PackageChecksumsAndEntry -PackageInfo $packageInfo
        if (-not [string]::IsNullOrWhiteSpace($SbomPath)) {
            Test-PluginSbom -Path $SbomPath -PackageInfo $packageInfo
        }

        Write-Host "Validated $([System.IO.Path]::GetFileName($packageInfo.PackagePath))"
    }
    finally {
        if (Test-Path -LiteralPath $packageInfo.TemporaryDirectory) {
            Remove-Item -LiteralPath $packageInfo.TemporaryDirectory -Recurse -Force
        }
    }

    return
}

if ([string]::IsNullOrWhiteSpace($TargetFramework) -or $TargetFramework -notmatch '^net\d+\.\d+$') {
    throw "-TargetFramework must be supplied in a form such as 'net8.0'."
}

$resolvedProjectPath = Resolve-ExistingFile -Path $ProjectPath -Description 'Project file'
$projectDirectory = [System.IO.Path]::GetDirectoryName($resolvedProjectPath)

if ([string]::IsNullOrWhiteSpace($PluginVersion)) {
    $PluginVersion = Get-MsBuildProperty -ResolvedProjectPath $resolvedProjectPath -PropertyName 'Version' -Framework $TargetFramework
}
if ([string]::IsNullOrWhiteSpace($TargetAbi)) {
    $TargetAbi = Get-MsBuildProperty -ResolvedProjectPath $resolvedProjectPath -PropertyName 'TargetAbi' -Framework $TargetFramework
}
if ([string]::IsNullOrWhiteSpace($JellyfinVersion)) {
    $JellyfinVersion = Get-MsBuildProperty -ResolvedProjectPath $resolvedProjectPath -PropertyName 'JellyfinVersion' -Framework $TargetFramework
}
if ([string]::IsNullOrWhiteSpace($HostingAbstractionsVersion)) {
    $HostingAbstractionsVersion = Get-MsBuildProperty -ResolvedProjectPath $resolvedProjectPath -PropertyName 'HostingAbstractionsVersion' -Framework $TargetFramework
}

$parsedPluginVersion = Assert-FourPartVersion -Value $PluginVersion -Description 'Plugin version'
$parsedTargetAbi = Assert-FourPartVersion -Value $TargetAbi -Description 'Target ABI'

try {
    $parsedJellyfinVersion = [System.Version]::Parse($JellyfinVersion)
    $null = [System.Version]::Parse($HostingAbstractionsVersion)
}
catch {
    throw "JellyfinVersion and HostingAbstractionsVersion must be numeric versions."
}

if ($parsedTargetAbi.Major -ne $parsedJellyfinVersion.Major -or
    $parsedTargetAbi.Minor -ne $parsedJellyfinVersion.Minor) {
    throw "Target ABI '$TargetAbi' and Jellyfin package version '$JellyfinVersion' must have the same major/minor version."
}

if ($parsedPluginVersion.Major -ne $parsedTargetAbi.Major -or
    $parsedPluginVersion.Minor -ne $parsedTargetAbi.Minor) {
    throw "Plugin version '$PluginVersion' must identify its Jellyfin ABI line '$TargetAbi' in the major/minor components."
}

if ([string]::IsNullOrWhiteSpace($DllPath)) {
    & dotnet restore $resolvedProjectPath "-property:TargetFramework=$TargetFramework"
    if ($LASTEXITCODE -ne 0) {
        throw "dotnet restore failed for '$TargetFramework'."
    }

    & dotnet build $resolvedProjectPath --configuration $Configuration --no-restore "-property:TargetFramework=$TargetFramework"
    if ($LASTEXITCODE -ne 0) {
        throw "dotnet build failed for '$TargetFramework'."
    }

    $DllPath = Join-Path $projectDirectory "bin/$Configuration/$TargetFramework/$PluginAssemblyFileName"
}

$resolvedDllPath = Resolve-ExistingFile -Path $DllPath -Description 'Built plugin DLL'
Test-ManagedPluginAssembly -Path $resolvedDllPath -ExpectedVersion $PluginVersion

if ([string]::IsNullOrWhiteSpace($Repository) -or $Repository -notmatch '^[A-Za-z0-9_.-]+/[A-Za-z0-9_.-]+$') {
    throw "-Repository must be a GitHub owner/repository slug."
}
if ([string]::IsNullOrWhiteSpace($ReleaseTag) -or $ReleaseTag -notmatch '^v\d+\.\d+\.\d+\.\d+$') {
    throw "-ReleaseTag must be a four-part version tag such as 'v10.11.0.1'."
}

if ([string]::IsNullOrWhiteSpace($Changelog)) {
    $Changelog = "Release $PluginVersion for Jellyfin $TargetAbi"
}

if ([string]::IsNullOrWhiteSpace($Timestamp)) {
    $releaseTimestamp = [System.DateTimeOffset]::UtcNow
}
else {
    try {
        $releaseTimestamp = [System.DateTimeOffset]::Parse(
            $Timestamp,
            [System.Globalization.CultureInfo]::InvariantCulture,
            [System.Globalization.DateTimeStyles]::AssumeUniversal
        )
    }
    catch {
        throw "-Timestamp is not a valid ISO-8601 timestamp: '$Timestamp'."
    }
}
$releaseTimestampText = $releaseTimestamp.ToUniversalTime().ToString(
    'yyyy-MM-ddTHH:mm:ss.fffZ',
    [System.Globalization.CultureInfo]::InvariantCulture
)

$resolvedOutputDirectory = Resolve-Directory -Path $OutputDirectory
$artifactBaseName = "jellyfin-plugin-audiotagger_$PluginVersion"
$packagePath = Join-Path $resolvedOutputDirectory "$artifactBaseName.zip"
$manifestEntryPath = Join-Path $resolvedOutputDirectory "$artifactBaseName.manifest-entry.json"
$stagingDirectory = Join-Path ([System.IO.Path]::GetTempPath()) "audiotagger-package-$([guid]::NewGuid().ToString('N'))"
$null = New-Item -ItemType Directory -Path $stagingDirectory

try {
    $stagedDllPath = Join-Path $stagingDirectory $PluginAssemblyFileName
    Copy-Item -LiteralPath $resolvedDllPath -Destination $stagedDllPath

    $pluginManifest = [ordered]@{
        category = $PluginCategory
        changelog = $Changelog
        description = $PluginDescription
        guid = $PluginId
        name = $PluginName
        overview = $PluginOverview
        owner = $PluginOwner
        targetAbi = $TargetAbi
        timestamp = $releaseTimestampText
        version = $PluginVersion
        status = [int]0
        autoUpdate = $true
        assemblies = @($PluginAssemblyFileName)
    }
    Write-Utf8Json -Value $pluginManifest -Path (Join-Path $stagingDirectory 'meta.json')

    if (Test-Path -LiteralPath $packagePath) {
        Remove-Item -LiteralPath $packagePath -Force
    }

    $archive = [System.IO.Compression.ZipFile]::Open($packagePath, [System.IO.Compression.ZipArchiveMode]::Create)
    try {
        [System.IO.Compression.ZipFileExtensions]::CreateEntryFromFile(
            $archive,
            $stagedDllPath,
            $PluginAssemblyFileName,
            [System.IO.Compression.CompressionLevel]::Optimal
        ) | Out-Null
        [System.IO.Compression.ZipFileExtensions]::CreateEntryFromFile(
            $archive,
            (Join-Path $stagingDirectory 'meta.json'),
            'meta.json',
            [System.IO.Compression.CompressionLevel]::Optimal
        ) | Out-Null
    }
    finally {
        $archive.Dispose()
    }
}
finally {
    if (Test-Path -LiteralPath $stagingDirectory) {
        Remove-Item -LiteralPath $stagingDirectory -Recurse -Force
    }
}

$md5 = Get-Checksum -Path $packagePath -Algorithm MD5
$sha256 = Get-Checksum -Path $packagePath -Algorithm SHA256
$sha512 = Get-Checksum -Path $packagePath -Algorithm SHA512
$packageFileName = [System.IO.Path]::GetFileName($packagePath)
Write-ChecksumFile -Hash $md5 -FileName $packageFileName -Path "$packagePath.md5"
Write-ChecksumFile -Hash $sha256 -FileName $packageFileName -Path "$packagePath.sha256"
Write-ChecksumFile -Hash $sha512 -FileName $packageFileName -Path "$packagePath.sha512"

$catalogEntry = [ordered]@{
    version = $PluginVersion
    changelog = $Changelog
    targetAbi = $TargetAbi
    sourceUrl = "https://github.com/$Repository/releases/download/$ReleaseTag/$packageFileName"
    checksum = $md5
    timestamp = $releaseTimestampText
}
Write-Utf8Json -Value $catalogEntry -Path $manifestEntryPath

$generatedSbomPath = $null
if ($GenerateSbom) {
    if ([string]::IsNullOrWhiteSpace($NuGetLockPath)) {
        $NuGetLockPath = Join-Path $projectDirectory "obj/packages.$TargetFramework.lock.json"
    }
    $generatedSbomPath = Join-Path $resolvedOutputDirectory "$artifactBaseName.sbom.cdx.json"
    New-PluginSbom `
        -LockFilePath $NuGetLockPath `
        -ResolvedDllPath $resolvedDllPath `
        -ResolvedProjectPath $resolvedProjectPath `
        -DestinationPath $generatedSbomPath `
        -Framework $TargetFramework `
        -Version $PluginVersion `
        -Abi $TargetAbi `
        -JellyfinPackageVersion $JellyfinVersion `
        -HostingPackageVersion $HostingAbstractionsVersion
}

$packageInfo = Read-PluginPackage -Path $packagePath
try {
    $null = Test-PackageChecksumsAndEntry -PackageInfo $packageInfo
    if ($GenerateSbom) {
        Test-PluginSbom -Path $generatedSbomPath -PackageInfo $packageInfo
    }
}
finally {
    if (Test-Path -LiteralPath $packageInfo.TemporaryDirectory) {
        Remove-Item -LiteralPath $packageInfo.TemporaryDirectory -Recurse -Force
    }
}

Write-Host "Created and validated $packageFileName for $TargetFramework / Jellyfin $TargetAbi"
