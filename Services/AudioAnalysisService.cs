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
    /// <returns>A list of audio tags to apply.</returns>
    public List<string> AnalyzeAudioStreams(Movie movie, PluginConfiguration config)
    {
        var allTags = new HashSet<string>(StringComparer.Ordinal);
        var mediaSources = movie.GetMediaSources(false)?.ToList();

        if (mediaSources is null || mediaSources.Count == 0)
        {
            if (config.VerboseLogging)
            {
                _logger.LogInformation("No media sources found for movie: {MovieName}", movie.Name);
            }

            return new List<string>();
        }

        foreach (var mediaSource in mediaSources)
        {
            var audioStreams = mediaSource.MediaStreams?.Where(s => s.Type == MediaStreamType.Audio) ?? Enumerable.Empty<MediaStream>();

            foreach (var stream in audioStreams)
            {
                var streamTags = AudioStreamTagClassifier.Classify(
                    stream.Channels,
                    stream.Codec,
                    stream.Title,
                    stream.Profile,
                    config.MinimumChannels,
                    config.OnlyMultichannelAudio);

                foreach (var tag in streamTags)
                {
                    allTags.Add(tag);
                }

                if (config.VerboseLogging)
                {
                    _logger.LogInformation(
                        "Movie: {MovieName}, Stream: codec={Codec}, profile={Profile}, channels={Channels}, title='{Title}' -> Tags: {Tags}",
                        movie.Name,
                        stream.Codec ?? "Unknown",
                        stream.Profile ?? "Unknown",
                        stream.Channels ?? 0,
                        stream.Title ?? "",
                        string.Join(", ", streamTags));
                }
            }
        }

        var result = allTags.ToList();
        result.Sort(StringComparer.Ordinal);

        if (config.VerboseLogging)
        {
            _logger.LogInformation("Final audio tags for {MovieName}: {Tags}", movie.Name, string.Join(", ", result));
        }

        return result;
    }
}
