using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Plugin.AudioTagger.Services;
using MediaBrowser.Controller.Entities.Movies;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.Plugins;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.AudioTagger;

/// <summary>
/// Server entry point for the Audio Tagger plugin.
/// </summary>
public class ServerEntryPoint : IHostedService
{
    private readonly ILibraryManager _libraryManager;
    private readonly AudioAnalysisService _audioAnalysisService;
    private readonly ILogger<ServerEntryPoint> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="ServerEntryPoint"/> class.
    /// </summary>
    /// <param name="libraryManager">Instance of the <see cref="ILibraryManager"/> interface.</param>
    /// <param name="audioAnalysisService">Instance of the <see cref="AudioAnalysisService"/> class.</param>
    /// <param name="logger">Instance of the <see cref="ILogger{ServerEntryPoint}"/> interface.</param>
    public ServerEntryPoint(
        ILibraryManager libraryManager,
        AudioAnalysisService audioAnalysisService,
        ILogger<ServerEntryPoint> logger)
    {
        _libraryManager = libraryManager;
        _audioAnalysisService = audioAnalysisService;
        _logger = logger;
    }

    /// <inheritdoc />
    public Task StartAsync(CancellationToken cancellationToken)
    {
        _libraryManager.ItemAdded += OnItemAdded;
        _libraryManager.ItemUpdated += OnItemUpdated;

        _logger.LogInformation("Audio Tagger plugin started. Monitoring library for new and updated movies.");

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task StopAsync(CancellationToken cancellationToken)
    {
        _libraryManager.ItemAdded -= OnItemAdded;
        _libraryManager.ItemUpdated -= OnItemUpdated;

        _logger.LogInformation("Audio Tagger plugin stopped.");

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public void Dispose()
    {
        // Cleanup handled in StopAsync
    }

    /// <summary>
    /// Called when an item is added to the library.
    /// </summary>
    /// <param name="sender">The sender.</param>
    /// <param name="e">The item change event args.</param>
    private async void OnItemAdded(object? sender, ItemChangeEventArgs e)
    {
        if (e.Item is Movie movie)
        {
            await ProcessMovieAsync(movie, "added");
        }
    }

    /// <summary>
    /// Called when an item is updated in the library.
    /// </summary>
    /// <param name="sender">The sender.</param>
    /// <param name="e">The item change event args.</param>
    private async void OnItemUpdated(object? sender, ItemChangeEventArgs e)
    {
        if (e.Item is Movie movie)
        {
            await ProcessMovieAsync(movie, "updated");
        }
    }

    /// <summary>
    /// Processes a movie for audio tagging.
    /// </summary>
    /// <param name="movie">The movie to process.</param>
    /// <param name="action">The action that triggered this processing (added/updated).</param>
    private async Task ProcessMovieAsync(Movie movie, string action)
    {
        try
        {
            var config = Plugin.Instance?.Configuration;
            if (config == null || !config.Enabled)
            {
                return;
            }

            // Check if this movie is in a monitored library
            var libraryName = movie.GetParent()?.Name;
            if (libraryName != null && !config.MonitoredLibraries.Contains(libraryName))
            {
                if (config.VerboseLogging)
                {
                    _logger.LogDebug("Skipping movie {MovieName} in unmonitored library: {LibraryName}", movie.Name, libraryName);
                }
                return;
            }

            _logger.LogInformation("Processing {Action} movie: {MovieName} in library: {LibraryName}", action, movie.Name, libraryName);

            // Analyze audio streams
            var suggestedTags = _audioAnalysisService.AnalyzeAudioStreams(movie, config);

            if (!suggestedTags.Any())
            {
                _logger.LogDebug("No audio tags needed for movie: {MovieName}", movie.Name);
                return;
            }

            // Get current tags
            var currentTags = movie.Tags?.ToList() ?? new List<string>();
            var tagsToAdd = suggestedTags.Where(tag => !currentTags.Contains(tag)).ToList();

            if (!tagsToAdd.Any())
            {
                _logger.LogDebug("Movie {MovieName} already has all suggested audio tags", movie.Name);
                return;
            }

            // Add new tags
            var newTags = currentTags.Concat(tagsToAdd).ToList();
            movie.Tags = newTags.ToArray();

            // Save the movie
            await _libraryManager.UpdateItemAsync(movie, movie.GetParent(), ItemUpdateType.MetadataEdit, CancellationToken.None);

            _logger.LogInformation(
                "Added {TagCount} audio tags to movie {MovieName}: {Tags}",
                tagsToAdd.Count,
                movie.Name,
                string.Join(", ", tagsToAdd));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing movie {MovieName} for audio tagging", movie.Name);
        }
    }
}
