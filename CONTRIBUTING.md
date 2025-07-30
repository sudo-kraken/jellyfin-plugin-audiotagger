# Contributing to Jellyfin Audio Tagger Plugin

Thank you for your interest in contributing to the Jellyfin Audio Tagger Plugin!

## Development Setup

1. Clone the repository:
   ```bash
   git clone https://github.com/sudo-kraken/jellyfin-plugin-audiotagger.git
   cd jellyfin-plugin-audiotagger
   ```

2. Install .NET 8.0 SDK from [Microsoft's website](https://dotnet.microsoft.com/download/dotnet/8.0)

3. Build the project:
   ```bash
   dotnet build --configuration Release
   ```

## Testing

1. Create a test library in Jellyfin with 1-2 movies
2. Configure the plugin to only monitor your test library
3. Install the plugin (starts disabled by default)
4. Enable via Dashboard → Plugins → Audio Tagger
5. Test on your sample movies

## Code Style

- Follow C# naming conventions
- Add XML documentation for public methods
- Include error handling with appropriate logging
- Write unit tests for new features

## Pull Requests

1. Fork the repository
2. Create a feature branch: `git checkout -b feature/your-feature`
3. Make your changes
4. Test thoroughly
5. Submit a pull request with a clear description

## Reporting Issues

Please include:
- Jellyfin version
- Plugin version
- Steps to reproduce
- Expected vs actual behavior
- Relevant log entries (with verbose logging enabled)

## Feature Requests

Open an issue with:
- Clear description of the requested feature
- Use case/rationale
- Proposed implementation (if you have ideas)

Thank you for contributing!
