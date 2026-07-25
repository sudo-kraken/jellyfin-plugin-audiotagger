using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Jellyfin.Plugin.AudioTagger.Services;

/// <summary>
/// Classifies an audio stream without depending on Jellyfin runtime types.
/// </summary>
internal static class AudioStreamTagClassifier
{
    private const string FivePointOneTag = "_5.1";
    private const string SevenPointOneTag = "_7.1";
    private const string SevenPointOnePointTwoTag = "_7.1.2";
    private const string Ac3Tag = "_AC3";
    private const string Eac3Tag = "_EAC3";
    private const string TrueHdTag = "_TrueHD";
    private const string DtsTag = "_DTS";
    private const string DtsHdMaTag = "_DTS-HD_MA";
    private const string LpcmTag = "_LPCM";
    private const string OpusTag = "_Opus";
    private const string AtmosTag = "_Atmos";
    private const string DtsXTag = "_DTSX";
    private const string ImaxTag = "_IMAX";
    private const string LosslessTag = "_Lossless";
    private const string LossyTag = "_Lossy";

    /// <summary>
    /// Gets every tag owned and reconciled by this plugin.
    /// </summary>
    internal static IReadOnlySet<string> ManagedTags { get; } = new HashSet<string>(
        new[]
        {
            FivePointOneTag,
            SevenPointOneTag,
            SevenPointOnePointTwoTag,
            Ac3Tag,
            Eac3Tag,
            TrueHdTag,
            DtsTag,
            DtsHdMaTag,
            LpcmTag,
            OpusTag,
            AtmosTag,
            DtsXTag,
            ImaxTag,
            LosslessTag,
            LossyTag,
        },
        StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Determines the canonical tags for one audio stream.
    /// </summary>
    /// <param name="channels">The number of audio channels, if known.</param>
    /// <param name="codec">The codec reported by Jellyfin.</param>
    /// <param name="title">The stream title.</param>
    /// <param name="profile">The stream profile.</param>
    /// <param name="minimumChannels">The configured minimum number of channels.</param>
    /// <param name="onlyMultichannelAudio">
    /// Whether the stream must also contain at least six channels.
    /// </param>
    /// <returns>A sorted collection of canonical tags.</returns>
    internal static IReadOnlyList<string> Classify(
        int? channels,
        string? codec,
        string? title,
        string? profile,
        int minimumChannels,
        bool onlyMultichannelAudio)
    {
        var channelCount = channels ?? 0;
        var configuredMinimum = Math.Max(minimumChannels, 1);
        var requiredChannels = onlyMultichannelAudio
            ? Math.Max(configuredMinimum, 6)
            : configuredMinimum;

        if (channelCount < requiredChannels)
        {
            return Array.Empty<string>();
        }

        var values = new[]
        {
            SearchValue.Create(codec),
            SearchValue.Create(title),
            SearchValue.Create(profile),
        };
        var tags = new HashSet<string>(StringComparer.Ordinal);

        AddChannelLayoutTag(tags, channelCount);

        var isEac3 = ContainsCompact(values, "eac3")
            || ContainsCompact(values, "ec3")
            || ContainsCompact(values, "dolbydigitalplus");
        var isAc3 = !isEac3
            && (ContainsCompact(values, "ac3") || ContainsCompact(values, "dolbydigital"));
        var isTrueHd = ContainsCompact(values, "truehd");
        var isDtsHdMa = ContainsCompact(values, "dtshdma")
            || ContainsCompact(values, "dtshdmasteraudio");
        var isDtsX = ContainsCompact(values, "dtsx");
        var isDts = isDtsHdMa
            || isDtsX
            || ContainsTokenStartingWith(values, "dts")
            || IsCodecAlias(codec, "dca");
        var isLpcm = ContainsToken(values, "lpcm")
            || ContainsTokenStartingWith(values, "pcm")
            || ContainsCompact(values, "linearpcm");
        var isOpus = ContainsToken(values, "opus");

        if (isEac3)
        {
            tags.Add(Eac3Tag);
        }
        else if (isAc3)
        {
            tags.Add(Ac3Tag);
        }

        if (isTrueHd)
        {
            tags.Add(TrueHdTag);
        }

        if (isDts)
        {
            tags.Add(DtsTag);
        }

        if (isDtsHdMa)
        {
            tags.Add(DtsHdMaTag);
        }

        if (isLpcm)
        {
            tags.Add(LpcmTag);
        }

        if (isOpus)
        {
            tags.Add(OpusTag);
        }

        if (ContainsCompact(values, "atmos"))
        {
            tags.Add(AtmosTag);
        }

        if (isDtsX)
        {
            tags.Add(DtsXTag);
        }

        if (ContainsCompact(values, "imax"))
        {
            tags.Add(ImaxTag);
        }

        if (isTrueHd || isDtsHdMa || isLpcm || IsExplicitlyLossless(values, codec))
        {
            tags.Add(LosslessTag);
        }
        else if (isEac3 || isAc3 || isDts || isOpus || IsExplicitlyLossy(values))
        {
            tags.Add(LossyTag);
        }

        return tags.OrderBy(tag => tag, StringComparer.Ordinal).ToArray();
    }

    private static void AddChannelLayoutTag(HashSet<string> tags, int channels)
    {
        switch (channels)
        {
            case 6:
                tags.Add(FivePointOneTag);
                break;
            case 8:
                tags.Add(SevenPointOneTag);
                break;
            case 10:
                tags.Add(SevenPointOnePointTwoTag);
                break;
        }
    }

    private static bool IsExplicitlyLossless(SearchValue[] values, string? codec)
    {
        return ContainsToken(values, "flac")
            || ContainsToken(values, "alac")
            || ContainsToken(values, "ape")
            || ContainsCompact(values, "wavpack")
            || IsCodecAlias(codec, "wv");
    }

    private static bool IsExplicitlyLossy(SearchValue[] values)
    {
        return ContainsToken(values, "aac")
            || ContainsCompact(values, "heaac")
            || ContainsToken(values, "adts")
            || ContainsToken(values, "mp4a")
            || ContainsToken(values, "mp3")
            || ContainsToken(values, "mp2")
            || ContainsToken(values, "vorbis")
            || ContainsToken(values, "amr")
            || ContainsToken(values, "speex")
            || ContainsToken(values, "cook")
            || ContainsTokenStartingWith(values, "atrac");
    }

    private static bool IsCodecAlias(string? codec, string alias)
    {
        return string.Equals(codec?.Trim(), alias, StringComparison.OrdinalIgnoreCase);
    }

    private static bool ContainsCompact(SearchValue[] values, string value)
    {
        return values.Any(candidate =>
            candidate.Compact.Contains(value, StringComparison.Ordinal));
    }

    private static bool ContainsToken(SearchValue[] values, string value)
    {
        return values.Any(candidate =>
            candidate.Tokens.Contains(value, StringComparer.Ordinal));
    }

    private static bool ContainsTokenStartingWith(SearchValue[] values, string value)
    {
        return values.Any(candidate =>
            candidate.Tokens.Any(token => token.StartsWith(value, StringComparison.Ordinal)));
    }

    private sealed class SearchValue
    {
        private SearchValue(string compact, string[] tokens)
        {
            Compact = compact;
            Tokens = tokens;
        }

        internal string Compact { get; }

        internal string[] Tokens { get; }

        internal static SearchValue Create(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return new SearchValue(string.Empty, Array.Empty<string>());
            }

            var compact = new StringBuilder(value.Length);
            var tokenized = new StringBuilder(value.Length);
            var previousWasSeparator = true;

            foreach (var character in value)
            {
                if (char.IsLetterOrDigit(character))
                {
                    var normalizedCharacter = char.ToLowerInvariant(character);
                    compact.Append(normalizedCharacter);
                    tokenized.Append(normalizedCharacter);
                    previousWasSeparator = false;
                }
                else if (!previousWasSeparator)
                {
                    tokenized.Append(' ');
                    previousWasSeparator = true;
                }
            }

            return new SearchValue(
                compact.ToString(),
                tokenized
                    .ToString()
                    .Split(' ', StringSplitOptions.RemoveEmptyEntries));
        }
    }
}
