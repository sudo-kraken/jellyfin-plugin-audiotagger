<div align="center">
<img src="docs/assets/logo.png" align="center" width="144px" height="144px"/>

### Jellyfin Audio Tagger Plugin

_An automatic audio tagging plugin for Jellyfin that analyses movie audio streams and adds descriptive tags based on channel layout, codec and audio quality._
</div>

<div align="center">

[![Jellyfin Version](https://img.shields.io/badge/Jellyfin-10.10.6%E2%80%9310.11.11-blue?style=for-the-badge)](https://jellyfin.org/)
[![.NET](https://img.shields.io/badge/.NET-8.0%20%7C%209.0-512BD4?logo=dotnet&logoColor=white&style=for-the-badge)](https://dotnet.microsoft.com/)

[![OpenSSF Scorecard](https://img.shields.io/ossf-scorecard/github.com/sudo-kraken/jellyfin-plugin-audiotagger?label=openssf%20scorecard&style=for-the-badge)](https://scorecard.dev/viewer/?uri=github.com/sudo-kraken/jellyfin-plugin-audiotagger)

</div>

## Contents

- [Overview](#overview)
- [Architecture at a glance](#architecture-at-a-glance)
- [Features](#features)
- [Generated tags](#generated-tags)
- [Installation](#installation)
  - [Method 1 Plugin repository recommended](#method-1-plugin-repository-recommended)
  - [Method 2 Manual installation](#method-2-manual-installation)
- [Configuration](#configuration)
  - [Settings](#settings)
  - [Default settings](#default-settings)
- [Examples](#examples)
- [Development](#development)
- [Compatibility](#compatibility)
- [Troubleshooting](#troubleshooting)
- [Licence](#licence)
- [Security](#security)
- [Contributing](#contributing)
- [Support](#support)

## Overview

Audio Tagger watches your selected libraries and automatically reconciles audio tags on films based on the streams it finds. It preserves tags it does not own and is safe to enable on existing libraries.

## Architecture at a glance

- Runs as a Jellyfin plugin
- Hooks into library scan and item update events
- Inspects all audio streams per title
- Computes tags from channel layout, codec and quality heuristics
- Adds tags to the item without modifying other fields

## Features

- **Automatic processing**: tags movies when added or updated
- **Smart analysis**: inspects all audio streams for a title
- **Comprehensive tags**: channel layout, codec, quality and special formats
- **Configurable**: select libraries, minimum channels and multichannel-only mode
- **Safe reconciliation**: removes stale Audio Tagger tags while preserving unrelated tags and metadata

## Generated tags

### Channel layout
- `_5.1` 6 channels
- `_7.1` 8 channels
- `_7.1.2` 10 channels

### Audio codecs
- `_AC3` Dolby Digital
- `_EAC3` Dolby Digital Plus
- `_TrueHD` Dolby TrueHD
- `_DTS` DTS
- `_DTS-HD_MA` DTS-HD Master Audio
- `_LPCM` Linear PCM
- `_Opus` Opus

### Object-based audio
- `_Atmos` Dolby Atmos
- `_DTSX` DTS:X

### Quality indicators
- `_Lossless` TrueHD, DTS-HD MA, LPCM and recognised lossless codecs
- `_Lossy` recognised lossy codecs

### Special formats
- `_IMAX` IMAX Enhanced

## Installation

### Method 1 Plugin repository recommended

1. **Add the repository**
   - Dashboard → Plugins → Repositories → **+**
   - Repository Name: `Audio Tagger`
   - Repository URL: `https://raw.githubusercontent.com/sudo-kraken/jellyfin-plugin-audiotagger/main/manifest.json`
   - Save

2. **Install the plugin**
   - Dashboard → Plugins → Catalog
   - Find **Audio Tagger** in **Metadata**
   - Install, then **restart Jellyfin**

3. **Configure**
   - Dashboard → Plugins → **Audio Tagger**
   - Enable and adjust settings

> [!NOTE]  
> The plugin starts **disabled by default** for safe testing. Enable it after configuration.

### Method 2 Manual installation

1. **Download the release asset for your Jellyfin ABI**
   - Go to the [Releases page](https://github.com/sudo-kraken/jellyfin-plugin-audiotagger/releases)
   - Jellyfin 10.10: download the asset whose version starts with `10.10.6`
   - Jellyfin 10.11: download the asset whose version starts with `10.11.0`

2. **Extract to the plugins directory**
   - **Windows** `%ProgramData%\Jellyfin\Server\plugins\AudioTagger\`
   - **Linux** `/var/lib/jellyfin/plugins/AudioTagger/`
   - **Docker** `/config/plugins/AudioTagger/`

3. **Restart Jellyfin**, then configure via Dashboard → Plugins → **Audio Tagger**

## Configuration

### Settings

- **Enable Audio Tagger** turn the plugin on or off
- **Only tag multichannel audio** skip stereo content recommended
- **Minimum channels** minimum channel count to tag
- **Monitored libraries** libraries to process
- **Verbose logging** extra detail in logs for debugging

### Default settings

- Enabled `false`
- Only multichannel `true`
- Minimum channels `6` 5.1+
- Monitored libraries none
- Verbose logging `false`

## Examples

**Premium 4K movie**

```
Audio: 7.1.2 Dolby Atmos TrueHD
Tags: _7.1.2, _TrueHD, _Atmos, _Lossless
```

**Standard Blu-ray**

```
Audio: 5.1 DTS-HD Master Audio
Tags: _5.1, _DTS, _DTS-HD_MA, _Lossless
```

**Multiple audio streams**

```
Stream 1: 7.1 DTS-HD MA
Stream 2: 5.1 Dolby Digital
Tags: _7.1, _5.1, _DTS, _DTS-HD_MA, _AC3, _Lossless, _Lossy
```

## Development

Install the .NET 8 and .NET 9 SDKs, then build and test both ABI variants:

```bash
git clone https://github.com/sudo-kraken/jellyfin-plugin-audiotagger.git
cd jellyfin-plugin-audiotagger
dotnet restore Jellyfin.Plugin.AudioTagger.sln
dotnet build Jellyfin.Plugin.AudioTagger.sln --configuration Release --no-restore
dotnet test Jellyfin.Plugin.AudioTagger.sln --configuration Release --no-build --no-restore
```

The plugin DLLs are written to `bin/Release/net8.0/` and `bin/Release/net9.0/`.
Do not copy Jellyfin or Microsoft.Extensions assemblies into the plugin directory.
On Windows, `build.bat` also runs the tests and creates both validated ZIPs and
their checksums under `release/v<version>/`.

## Compatibility

Jellyfin changed a binary API and its runtime between 10.10 and 10.11, so one
DLL cannot safely support both lines. Releases contain distinct artifacts and
the plugin catalog selects the highest compatible one.

| Jellyfin Server | Plugin target | Runtime | Verification |
| --- | --- | --- | --- |
| 10.10.6–10.10.7 | `10.10.6.x` / ABI `10.10.6.0` | .NET 8 | API build + unit tests |
| 10.11.0–10.11.11 | `10.11.0.x` / ABI `10.11.0.0` | .NET 9 | API build + unit tests |
| 12.0 previews | No release artifact | .NET 10 | CI canary only |

Every published stable Jellyfin version in the tested ranges is covered by the
API compatibility build matrix. Preview releases are not supported until their
ABI is stable.

Supported platforms are Windows, Linux, macOS and Docker.

## Troubleshooting

- Enable **Verbose logging** in the plugin settings and reproduce the issue
- Check **Dashboard → Logs** and the Jellyfin server logs on disk
- Confirm the library is included in **Monitored libraries**
- Ensure **Minimum channels** aligns with the media you expect to tag

## Licence

This project is licensed under the MIT Licence. See the [LICENSE](LICENSE) file for details.

## Security

If you discover a security issue, follow the private reporting guidance in [SECURITY.md](SECURITY.md).

## Contributing

Feel free to open issues or submit pull requests if you have suggestions or improvements.  
See [CONTRIBUTING.md](CONTRIBUTING.md)

## Support

Open an [issue](/../../issues) with as much detail as possible, including Jellyfin version, platform, plugin version and relevant log excerpts.
