using System.Collections.Generic;
using Jellyfin.Plugin.AudioTagger.Services;
using Xunit;

namespace Jellyfin.Plugin.AudioTagger.Tests;

public class AudioStreamTagClassifierTests
{
    [Fact]
    public void ManagedTags_ContainsEveryCanonicalTagCaseInsensitively()
    {
        var expected = new[]
        {
            "_5.1",
            "_7.1",
            "_7.1.2",
            "_AC3",
            "_EAC3",
            "_TrueHD",
            "_DTS",
            "_DTS-HD_MA",
            "_LPCM",
            "_Opus",
            "_Atmos",
            "_DTSX",
            "_IMAX",
            "_Lossless",
            "_Lossy",
        };

        Assert.Equal(expected.Length, AudioStreamTagClassifier.ManagedTags.Count);
        Assert.All(expected, tag => Assert.Contains(tag, AudioStreamTagClassifier.ManagedTags));
        Assert.Contains("_truehd", AudioStreamTagClassifier.ManagedTags);
    }

    [Theory]
    [InlineData(6, "_5.1")]
    [InlineData(8, "_7.1")]
    [InlineData(10, "_7.1.2")]
    public void Classify_AddsCanonicalChannelLayoutTags(int channels, string expected)
    {
        var tags = Classify(channels, codec: "unknown");

        Assert.Contains(expected, tags);
    }

    [Theory]
    [InlineData(7)]
    [InlineData(9)]
    [InlineData(12)]
    public void Classify_ClassifiesCodecsWithoutARecognizedChannelLayout(int channels)
    {
        var tags = Classify(channels, codec: "truehd");

        Assert.Contains("_TrueHD", tags);
        Assert.Contains("_Lossless", tags);
        Assert.DoesNotContain("_5.1", tags);
        Assert.DoesNotContain("_7.1", tags);
        Assert.DoesNotContain("_7.1.2", tags);
    }

    [Fact]
    public void Classify_AlwaysEnforcesMinimumChannels()
    {
        var tags = Classify(
            channels: 5,
            codec: "eac3",
            minimumChannels: 6,
            onlyMultichannelAudio: false);

        Assert.Empty(tags);
    }

    [Fact]
    public void Classify_ClampsInvalidMinimumChannels()
    {
        var tags = Classify(
            channels: 0,
            codec: "eac3",
            minimumChannels: -1,
            onlyMultichannelAudio: false);

        Assert.Empty(tags);
    }

    [Fact]
    public void Classify_AllowsStereoWhenConfiguredMinimumIsMet()
    {
        var tags = Classify(
            channels: 2,
            codec: "flac",
            minimumChannels: 2,
            onlyMultichannelAudio: false);

        Assert.Equal(new[] { "_Lossless" }, tags);
    }

    [Theory]
    [InlineData(2)]
    [InlineData(5)]
    public void Classify_OnlyMultichannelRequiresAtLeastSixChannels(int channels)
    {
        var tags = Classify(
            channels,
            codec: "eac3",
            minimumChannels: 2,
            onlyMultichannelAudio: true);

        Assert.Empty(tags);
    }

    [Fact]
    public void Classify_OnlyMultichannelDoesNotOverrideAHigherMinimum()
    {
        var tags = Classify(
            channels: 7,
            codec: "eac3",
            minimumChannels: 8,
            onlyMultichannelAudio: true);

        Assert.Empty(tags);
    }

    [Theory]
    [InlineData("eac3", null, null, "_EAC3", "_Lossy")]
    [InlineData("ac-3", null, null, "_AC3", "_Lossy")]
    [InlineData("truehd", null, null, "_TrueHD", "_Lossless")]
    [InlineData("dts", null, null, "_DTS", "_Lossy")]
    [InlineData("dca", null, null, "_DTS", "_Lossy")]
    [InlineData("pcm_s24le", null, null, "_LPCM", "_Lossless")]
    [InlineData("opus", null, null, "_Opus", "_Lossy")]
    [InlineData("unknown", "Dolby Digital Plus", null, "_EAC3", "_Lossy")]
    [InlineData("unknown", null, "TrueHD", "_TrueHD", "_Lossless")]
    public void Classify_DetectsCanonicalCodecTags(
        string? codec,
        string? title,
        string? profile,
        string codecTag,
        string qualityTag)
    {
        var tags = Classify(codec: codec, title: title, profile: profile);

        Assert.Contains(codecTag, tags);
        Assert.Contains(qualityTag, tags);
    }

    [Fact]
    public void Classify_DoesNotMistakeEac3ForAc3()
    {
        var tags = Classify(codec: "eac3");

        Assert.Contains("_EAC3", tags);
        Assert.DoesNotContain("_AC3", tags);
    }

    [Theory]
    [InlineData(null, "Dolby Atmos", null, "_Atmos")]
    [InlineData(null, null, "Dolby Atmos", "_Atmos")]
    [InlineData(null, null, "DolbyDigitalPlusWithAtmos", "_Atmos")]
    [InlineData(null, "DTS:X", null, "_DTSX")]
    [InlineData(null, null, "DTS X", "_DTSX")]
    [InlineData(null, "IMAX Enhanced", null, "_IMAX")]
    [InlineData(null, null, "IMAX Enhanced", "_IMAX")]
    [InlineData(null, null, "ImaxEnhanced", "_IMAX")]
    public void Classify_DetectsObjectAndSpecialTagsFromTitleOrProfile(
        string? codec,
        string? title,
        string? profile,
        string expected)
    {
        var tags = Classify(codec: codec, title: title, profile: profile);

        Assert.Contains(expected, tags);
    }

    [Theory]
    [InlineData("dca", null, "DTS-HD MA")]
    [InlineData("dts-hd ma", null, null)]
    [InlineData("unknown", "DTS HD Master Audio", null)]
    [InlineData("unknown", null, "DTS-HD Master Audio")]
    public void Classify_DetectsDtsHdMasterAudioFromAllMetadata(
        string? codec,
        string? title,
        string? profile)
    {
        var tags = Classify(codec: codec, title: title, profile: profile);

        Assert.Contains("_DTS", tags);
        Assert.Contains("_DTS-HD_MA", tags);
        Assert.Contains("_Lossless", tags);
        Assert.DoesNotContain("_Lossy", tags);
    }

    [Theory]
    [InlineData("flac")]
    [InlineData("alac")]
    [InlineData("ape")]
    [InlineData("wavpack")]
    [InlineData("wv")]
    public void Classify_AddsLosslessQualityWithoutInventingACodecTag(string codec)
    {
        var tags = Classify(codec: codec);

        Assert.Contains("_Lossless", tags);
        Assert.DoesNotContain("_AC3", tags);
        Assert.DoesNotContain("_EAC3", tags);
        Assert.DoesNotContain("_TrueHD", tags);
        Assert.DoesNotContain("_DTS", tags);
        Assert.DoesNotContain("_DTS-HD_MA", tags);
        Assert.DoesNotContain("_LPCM", tags);
        Assert.DoesNotContain("_Opus", tags);
    }

    [Theory]
    [InlineData("aac")]
    [InlineData("adts")]
    [InlineData("he-aac")]
    [InlineData("mp4a")]
    [InlineData("mp3")]
    [InlineData("mp2")]
    [InlineData("vorbis")]
    [InlineData("amr")]
    [InlineData("speex")]
    [InlineData("cook")]
    [InlineData("atrac3")]
    public void Classify_AddsLossyQualityForExplicitLossyFormats(string codec)
    {
        var tags = Classify(codec: codec);

        Assert.Contains("_Lossy", tags);
        Assert.DoesNotContain("_Lossless", tags);
    }

    [Fact]
    public void Classify_DoesNotMistakeAdtsForDts()
    {
        var tags = Classify(codec: "adts");

        Assert.Contains("_Lossy", tags);
        Assert.DoesNotContain("_DTS", tags);
    }

    [Fact]
    public void Classify_DoesNotGuessQualityForUnknownCodec()
    {
        var tags = Classify(codec: "mystery", title: "English", profile: "Custom");

        Assert.Equal(new[] { "_5.1" }, tags);
    }

    [Fact]
    public void Classify_ObjectMetadataAloneDoesNotGuessQuality()
    {
        var tags = Classify(codec: "unknown", profile: "Dolby Atmos");

        Assert.Contains("_Atmos", tags);
        Assert.DoesNotContain("_Lossless", tags);
        Assert.DoesNotContain("_Lossy", tags);
    }

    [Fact]
    public void Classify_UsesCodecTitleAndProfileTogether()
    {
        var tags = Classify(
            channels: 12,
            codec: "dca",
            title: "IMAX Enhanced",
            profile: "DTS-HD MA / DTS:X");

        Assert.Equal(
            new[] { "_DTS", "_DTS-HD_MA", "_DTSX", "_IMAX", "_Lossless" },
            tags);
    }

    private static IReadOnlyList<string> Classify(
        int? channels = 6,
        string? codec = null,
        string? title = null,
        string? profile = null,
        int minimumChannels = 6,
        bool onlyMultichannelAudio = true)
    {
        return AudioStreamTagClassifier.Classify(
            channels,
            codec,
            title,
            profile,
            minimumChannels,
            onlyMultichannelAudio);
    }
}
