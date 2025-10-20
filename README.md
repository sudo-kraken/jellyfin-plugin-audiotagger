<div align="center">
<img src="docs/assets/logo.png" align="center" width="144px" height="144px"/>

### Jellyfin Audio Tagger Plugin

_An automatic audio tagging plugin for Jellyfin that analyses movie audio streams and adds descriptive tags based on channel layout, codec and audio quality._
</div>

<div align="center">

[![Jellyfin Version](https://img.shields.io/badge/Jellyfin-10.9.0%2B-blue?style=for-the-badge)](https://jellyfin.org/)
[![.NET](https://img.shields.io/badge/.NET-8.0%2B-512BD4?logo=dotnet&logoColor=white&style=for-the-badge)](https://dotnet.microsoft.com/)

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

Audio Tagger watches your selected libraries and automatically adds tags to films, based on the audio streams it finds. It never edits other metadata and is safe to enable on existing libraries.

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
- **Safe**: only adds tags; does not alter other metadata

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
- `_Lossless` TrueHD, DTS-HD MA, LPCM
- `_Lossy` all other formats

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

1. **Download the latest release**
   - Go to the [Releases page](https://github.com/sudo-kraken/jellyfin-plugin-audiotagger/releases)
   - Download `jellyfin-plugin-audiotagger_x.x.x.zip`

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
- Monitored libraries `Test Movies`
- Verbose logging `true`

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

Build from source:

```bash
git clone https://github.com/sudo-kraken/jellyfin-plugin-audiotagger.git
cd jellyfin-plugin-audiotagger
# Requires .NET 8 SDK
dotnet build --configuration Release
```

Copy files from `bin/Release/net8.0/` to your Jellyfin plugins directory.

## Compatibility

- **Jellyfin** 10.9.0+
- **.NET** 8.0
- **Platforms** Windows, Linux, macOS and Docker

## Troubleshooting

- Enable **Verbose logging** in the plugin settings and reproduce the issue
- Check **Dashboard → Logs** and the Jellyfin server logs on disk
- Confirm the library is included in **Monitored libraries**
- Ensure **Minimum channels** aligns with the media you expect to tag

## Licence

This project is licensed under the MIT Licence. See the [LICENCE](LICENCE) file for details.

## Security

If you discover a security issue, please review and follow the guidance in [SECURITY.md](SECURITY.md), or open a private security-focused issue with minimal details and request a secure contact channel.

## Contributing

Feel free to open issues or submit pull requests if you have suggestions or improvements.  
See [CONTRIBUTING.md](CONTRIBUTING.md)

## Support

Open an [issue](/../../issues) with as much detail as possible, including Jellyfin version, platform, plugin version and relevant log excerpts.
