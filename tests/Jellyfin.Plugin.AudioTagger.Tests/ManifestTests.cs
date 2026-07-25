using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.RegularExpressions;
using Xunit;

namespace Jellyfin.Plugin.AudioTagger.Tests;

public partial class ManifestTests
{
    private static readonly string ManifestPath = Path.Combine(AppContext.BaseDirectory, "manifest.json");

    [Fact]
    public void CatalogManifest_HasValidInstallableVersions()
    {
        using var document = JsonDocument.Parse(File.ReadAllText(ManifestPath));
        var packages = document.RootElement;

        Assert.Equal(JsonValueKind.Array, packages.ValueKind);
        var package = Assert.Single(packages.EnumerateArray());
        Assert.Equal("33fc255a-be9b-11ef-993c-272469e0c801", package.GetProperty("guid").GetString());

        var seenVersions = new HashSet<Version>();
        var seenSources = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        Version? previousVersion = null;

        foreach (var release in package.GetProperty("versions").EnumerateArray())
        {
            var versionText = Assert.IsType<string>(release.GetProperty("version").GetString());
            var version = Version.Parse(versionText);
            Assert.True(seenVersions.Add(version), $"Duplicate plugin version: {version}");

            if (previousVersion is not null)
            {
                Assert.True(previousVersion > version, "Plugin versions must be ordered newest first.");
            }

            previousVersion = version;

            var targetAbi = Version.Parse(
                Assert.IsType<string>(release.GetProperty("targetAbi").GetString()));
            Assert.True(targetAbi >= new Version(10, 10, 6, 0));

            var sourceUrl = Assert.IsType<string>(release.GetProperty("sourceUrl").GetString());
            Assert.True(seenSources.Add(sourceUrl), $"Duplicate source URL: {sourceUrl}");
            Assert.EndsWith(
                $"jellyfin-plugin-audiotagger_{versionText}.zip",
                sourceUrl,
                StringComparison.Ordinal);

            var checksum = Assert.IsType<string>(release.GetProperty("checksum").GetString());
            Assert.Matches(Md5Pattern(), checksum);

            var timestamp = Assert.IsType<string>(release.GetProperty("timestamp").GetString());
            Assert.True(DateTimeOffset.TryParse(timestamp, out _), $"Invalid timestamp: {timestamp}");
        }
    }

    [GeneratedRegex("^[A-F0-9]{32}$", RegexOptions.CultureInvariant)]
    private static partial Regex Md5Pattern();
}
