using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Jellyfin.Plugin.AudioTagger.Configuration;
using MediaBrowser.Controller.Entities.Movies;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.MediaEncoding;
using MediaBrowser.Model.Entities;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.AudioTagger.Services;

/// <summary>
/// Service for analyzing audio streams and determining appropriate tags.
/// </summary>
public class AudioAnalysisService
{
    private readonly ILogger<AudioAnalysisService> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="AudioAnalysisService"/> class.
    /// </summary>
    /// <param name="logger">Instance of the <see cref="ILogger{AudioAnalysisService}"/> interface.</param>
    public AudioAnalysisService(ILogger<AudioAnalysisService> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Analyzes audio streams and returns appropriate tags.
    /// </summary>
    /// <param name="movie">The movie to analyze.</param>
    /// <param name="config">Plugin configuration.</param>
    /// <returns>A list of audio tags to add.</returns>
    public List<string> AnalyzeAudioStreams(Movie movie, PluginConfiguration config)
    {
        var allTags = new HashSet<string>();

        if (movie.GetMediaSources(false) == null || !movie.GetMediaSources(false).Any())
        {
            _logger.LogDebug("No media sources found for movie: {MovieName}", movie.Name);
            return new List<string>();
        }

        foreach (var mediaSource in movie.GetMediaSources(false))
        {
            var audioStreams = mediaSource.MediaStreams?.Where(s => s.Type == MediaStreamType.Audio) ?? Enumerable.Empty<MediaStream>();
            
            foreach (var stream in audioStreams)
            {
                var streamTags = DetermineTagsForStream(stream, config);
                foreach (var tag in streamTags)
                {
                    allTags.Add(tag);
                }

                if (config.VerboseLogging)
                {
                    _logger.LogInformation(
                        "Movie: {MovieName}, Stream: {Codec} {Channels}ch '{Title}' -> Tags: {Tags}",
                        movie.Name,
                        stream.Codec ?? "Unknown",
                        stream.Channels ?? 0,
                        stream.Title ?? "",
                        string.Join(", ", streamTags));
                }
            }
        }

        var result = allTags.ToList();
        result.Sort(); // Sort alphabetically for consistency

        if (config.VerboseLogging)
        {
            _logger.LogInformation("Final audio tags for {MovieName}: {Tags}", movie.Name, string.Join(", ", result));
        }

        return result;
    }

    /// <summary>
    /// Determines tags for a single audio stream.
    /// </summary>
    /// <param name="stream">The audio stream to analyze.</param>
    /// <param name="config">Plugin configuration.</param>
    /// <returns>A list of tags for this stream.</returns>
    private List<string> DetermineTagsForStream(MediaStream stream, PluginConfiguration config)
    {
        var tags = new HashSet<string>();
        var channels = stream.Channels ?? 0;
        var title = (stream.Title ?? string.Empty).ToLowerInvariant();
        var codec = (stream.Codec ?? string.Empty).ToLowerInvariant();

        // Skip streams that don't meet minimum channel requirement
        if (config.OnlyMultichannelAudio && channels < config.MinimumChannels)
        {
            return new List<string>();
        }

        // Channel layout tags (only for multichannel)
        if (channels == 6) tags.Add("_5.1");
        else if (channels == 8) tags.Add("_7.1");
        else if (channels == 10) tags.Add("_7.1.2");

        // Only add codec and quality tags if we have channel layout tags
        if (tags.Any())
        {
            // Codec tags
            if (codec.Contains("eac3")) tags.Add("_EAC3");
            else if (codec.Contains("ac3")) tags.Add("_AC3");
            else if (codec.Contains("truehd")) tags.Add("_TrueHD");
            else if (codec.Contains("dts"))
            {
                tags.Add("_DTS");
                if (title.Contains("dts-hd") || title.Contains("dts hd"))
                {
                    tags.Add("_DTS-HD_MA");
                }
            }
            else if (codec.Contains("pcm")) tags.Add("_LPCM");
            else if (codec.Contains("opus")) tags.Add("_Opus");

            // Object-based audio tags
            if (title.Contains("atmos")) tags.Add("_Atmos");
            if (title.Contains("dts:x") || title.Contains("dtsx")) tags.Add("_DTSX");

            // Special tags
            if (title.Contains("imax")) tags.Add("_IMAX");

            // Quality tags (only if we have other tags)
            if (tags.Any(t => t is "_TrueHD" or "_DTS-HD_MA" or "_LPCM"))
            {
                tags.Add("_Lossless");
            }
            else if (tags.Any())
            {
                tags.Add("_Lossy");
            }
        }

        return tags.ToList();
    }
}
