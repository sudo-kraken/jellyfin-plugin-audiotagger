# Jellyfin Audio Tagger Plugin

[![Build Status](https://github.com/sudo-kraken/jellyfin-plugin-audiotagger/actions/workflows/build.yml/badge.svg)](https://github.com/sudo-kraken/jellyfin-plugin-audiotagger/actions)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](https://opensource.org/licenses/MIT)
[![Jellyfin Version](https://img.shields.io/badge/Jellyfin-10.9.0%2B-blue)](https://jellyfin.org/)

An automatic audio tagging plugin for Jellyfin that analyzes movie audio streams and adds descriptive tags based on channel layout, codec, and audio quality.

## Features

- 🎬 **Automatic Processing**: Tags movies when added or updated
- 🔊 **Smart Analysis**: Analyzes all audio streams in each movie
- 🏷️ **Comprehensive Tags**: Channel layout, codec, quality, and special format tags
- ⚙️ **Configurable**: Choose which libraries to monitor and minimum channel requirements
- 🛡️ **Safe**: Only adds tags, never modifies other metadata

## Generated Tags

### Channel Layout
- `_5.1` (6 channels)
- `_7.1` (8 channels)
- `_7.1.2` (10 channels)

### Audio Codecs
- `_AC3` (Dolby Digital)
- `_EAC3` (Enhanced AC-3/Dolby Digital Plus)
- `_TrueHD` (Dolby TrueHD)
- `_DTS` (DTS)
- `_DTS-HD_MA` (DTS-HD Master Audio)
- `_LPCM` (Linear PCM)
- `_Opus` (Opus codec)

### Object-Based Audio
- `_Atmos` (Dolby Atmos)
- `_DTSX` (DTS:X)

### Quality Indicators
- `_Lossless` (TrueHD, DTS-HD MA, LPCM)
- `_Lossy` (All other formats)

### Special Formats
- `_IMAX` (IMAX Enhanced)

## Installation

### Method 1: Plugin Repository (Recommended)

1. **Add the repository**:
   - Go to **Dashboard** → **Plugins** → **Repositories**
   - Click **"+"** to add a new repository
   - **Repository Name**: `Audio Tagger`
   - **Repository URL**: `https://raw.githubusercontent.com/sudo-kraken/jellyfin-plugin-audiotagger/main/manifest.json`
   - Click **Save**

2. **Install the plugin**:
   - Go to **Dashboard** → **Plugins** → **Catalog**
   - Find **"Audio Tagger"** in the **Metadata** category
   - Click **Install**

3. **Restart Jellyfin**

4. **Configure the plugin**:
   - Go to **Dashboard** → **Plugins** → **Audio Tagger**
   - Enable the plugin and configure your settings

### Method 2: Manual Installation

1. **Download the latest release**:
   - Go to the [Releases page](https://github.com/sudo-kraken/jellyfin-plugin-audiotagger/releases)
   - Download `jellyfin-plugin-audiotagger_x.x.x.zip`

2. **Extract to plugins directory**:
2. **Extract to plugins directory**:
   - **Windows**: `%ProgramData%\Jellyfin\Server\plugins\AudioTagger\`
   - **Linux**: `/var/lib/jellyfin/plugins/AudioTagger/`
   - **Docker**: `/config/plugins/AudioTagger/`

3. **Restart Jellyfin**

4. **Configure the plugin**:
   - Go to **Dashboard** → **Plugins** → **Audio Tagger**
   - Enable the plugin and configure your settings

> ⚠️ **Note**: Plugin starts **disabled by default** for safe testing. Enable it after configuration.

## Advanced Installation

1. Clone and build from source (see below)
2. Follow Method 2 installation steps above

## Configuration

### Settings
- **Enable Audio Tagger**: Turn the plugin on/off
- **Only tag multichannel audio**: Skip stereo content (recommended)
- **Minimum Channels**: Set minimum channel count for tagging
- **Monitored Libraries**: Choose which libraries to process
- **Verbose Logging**: Enable detailed logs for debugging

### Default Settings

- Enabled: `false` (for safe testing)
- Only multichannel: `true` (skips stereo)
- Minimum channels: `6` (5.1+)
- Monitored libraries: `Test Movies` (for testing)
- Verbose logging: `true` (for debugging)

## Examples

**Premium 4K Movie:**
```
Audio: 7.1.2 Dolby Atmos TrueHD
Tags: _7.1.2, _TrueHD, _Atmos, _Lossless
```

**Standard Blu-ray:**
```
Audio: 5.1 DTS-HD Master Audio
Tags: _5.1, _DTS, _DTS-HD_MA, _Lossless
```

**Multiple Audio Streams:**
```
Stream 1: 7.1 DTS-HD MA
Stream 2: 5.1 Dolby Digital
Tags: _7.1, _5.1, _DTS, _DTS-HD_MA, _AC3, _Lossless, _Lossy
```

If you want to build from source or contribute:

1. Clone this repository:

   ```bash
   git clone https://github.com/sudo-kraken/jellyfin-plugin-audiotagger.git
   cd jellyfin-plugin-audiotagger
   ```

2. Install .NET 8.0 SDK from [Microsoft's website](https://dotnet.microsoft.com/download/dotnet/8.0)

3. Build the plugin:

   ```bash
   dotnet build --configuration Release
   ```

4. Copy files from `bin/Release/net8.0/` to your plugins directory

## Compatibility

- Jellyfin 10.9.0+
- .NET 8.0
- All platforms (Windows, Linux, macOS, Docker)

## Contributing

We welcome contributions! Please see [CONTRIBUTING.md](CONTRIBUTING.md) for guidelines.

## Support

- [Open an issue on GitHub](https://github.com/sudo-kraken/jellyfin-plugin-audiotagger/issues) for bugs or feature requests
- Check the [Jellyfin documentation](https://jellyfin.org/docs/) for general Jellyfin help

For issues, feature requests, or questions:
- Open an issue on GitHub
- Check Jellyfin logs for error details
- Enable verbose logging for debugging

## License

This project is licensed under the MIT License.
