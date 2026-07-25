using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Plugin.AudioTagger.Infrastructure;
using Jellyfin.Plugin.AudioTagger.Services;
using MediaBrowser.Controller.Entities.Movies;
using MediaBrowser.Controller.Library;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.AudioTagger;

/// <summary>
/// Server entry point for the Audio Tagger plugin.
/// </summary>
public sealed class ServerEntryPoint : IHostedService, IDisposable
{
  private readonly ILibraryManager _libraryManager;
  private readonly AudioAnalysisService _audioAnalysisService;
  private readonly ILogger<ServerEntryPoint> _logger;
  private readonly object _syncRoot = new();
  private readonly Dictionary<Guid, MovieProcessingRegistration> _processingMovies = new();
  private readonly HashSet<Guid> _selfUpdatingMovies = new();
  private Task _lifecycleCleanupTask = Task.CompletedTask;
  private CancellationTokenSource? _stoppingSource;
  private bool _acceptingEvents;
  private bool _started;
  private bool _disposed;

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
    cancellationToken.ThrowIfCancellationRequested();

    lock (_syncRoot)
    {
      ObjectDisposedException.ThrowIf(_disposed, this);

      if (_started)
      {
        return Task.CompletedTask;
      }

      _stoppingSource = new CancellationTokenSource();
      _acceptingEvents = true;
      _started = true;
      _libraryManager.ItemAdded += OnItemAdded;
      _libraryManager.ItemUpdated += OnItemUpdated;
    }

    _logger.LogInformation(
        "Audio Tagger plugin started. Monitoring library for new and updated movies.");

    return Task.CompletedTask;
  }

  /// <inheritdoc />
  public async Task StopAsync(CancellationToken cancellationToken)
  {
    CancellationTokenSource? stoppingSource;
    Task[] processingTasks;

    lock (_syncRoot)
    {
      if (!_started)
      {
        return;
      }

      _acceptingEvents = false;
      _started = false;
      _libraryManager.ItemAdded -= OnItemAdded;
      _libraryManager.ItemUpdated -= OnItemUpdated;

      stoppingSource = _stoppingSource;
      _stoppingSource = null;
      foreach (var registration in _processingMovies.Values)
      {
        registration.PendingRequests.Clear();
      }

      processingTasks = _processingMovies.Values
          .Select(static registration => registration.ProcessingTask)
          .ToArray();
    }

    stoppingSource?.Cancel();
    var cleanupTask = DrainAndDisposeAsync(processingTasks, stoppingSource);

    lock (_syncRoot)
    {
      _lifecycleCleanupTask = Task.WhenAll(_lifecycleCleanupTask, cleanupTask);
    }

    try
    {
      await cleanupTask.WaitAsync(cancellationToken).ConfigureAwait(false);
    }
    catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
    {
      _logger.LogWarning(
          "Audio Tagger plugin stop timed out while movie processing was still draining.");
      throw;
    }

    _logger.LogInformation("Audio Tagger plugin stopped.");
  }

  /// <inheritdoc />
  public void Dispose()
  {
    CancellationTokenSource? stoppingSource;
    Task[] processingTasks;
    Task lifecycleCleanupTask;

    lock (_syncRoot)
    {
      if (_disposed)
      {
        return;
      }

      _disposed = true;
      _acceptingEvents = false;

      if (_started)
      {
        _started = false;
        _libraryManager.ItemAdded -= OnItemAdded;
        _libraryManager.ItemUpdated -= OnItemUpdated;
      }

      stoppingSource = _stoppingSource;
      _stoppingSource = null;
      foreach (var registration in _processingMovies.Values)
      {
        registration.PendingRequests.Clear();
      }

      processingTasks = _processingMovies.Values
          .Select(static registration => registration.ProcessingTask)
          .ToArray();
      lifecycleCleanupTask = _lifecycleCleanupTask;
    }

    stoppingSource?.Cancel();

    try
    {
      Task.WhenAll(processingTasks.Append(lifecycleCleanupTask)).GetAwaiter().GetResult();
    }
    finally
    {
      stoppingSource?.Dispose();
    }
  }

  /// <summary>
  /// Called when an item is added to the library.
  /// </summary>
  /// <param name="sender">The sender.</param>
  /// <param name="e">The item change event args.</param>
  private void OnItemAdded(object? sender, ItemChangeEventArgs e)
  {
    if (e.Item is Movie movie)
    {
      QueueMovie(movie, "added");
    }
  }

  /// <summary>
  /// Called when an item is updated in the library.
  /// </summary>
  /// <param name="sender">The sender.</param>
  /// <param name="e">The item change event args.</param>
  private void OnItemUpdated(object? sender, ItemChangeEventArgs e)
  {
    if (e.Item is Movie movie)
    {
      QueueMovie(
          movie,
          "updated",
          e.UpdateReason == ItemUpdateType.MetadataEdit);
    }
  }

  /// <summary>
  /// Queues a movie for processing if it is not already being processed.
  /// </summary>
  /// <param name="movie">The movie to process.</param>
  /// <param name="action">The action that triggered processing.</param>
  /// <param name="isExpectedSelfUpdate">Whether the event matches the update reason used by this plugin.</param>
  private void QueueMovie(
      Movie movie,
      string action,
      bool isExpectedSelfUpdate = false)
  {
    var movieId = movie.Id;
    var request = new CoalescingWorkRequest<Movie>(movie, action);

    lock (_syncRoot)
    {
      var stoppingSource = _stoppingSource;
      var canAcceptWork = _acceptingEvents
          && stoppingSource is not null
          && !stoppingSource.IsCancellationRequested;
      var hasActiveRegistration = _processingMovies.TryGetValue(
          movieId,
          out var existingRegistration);
      var decision = CoalescingWorkScheduler.Decide(
          canAcceptWork,
          hasActiveRegistration,
          isExpectedSelfUpdate && _selfUpdatingMovies.Contains(movieId));

      if (decision == CoalescingWorkDecision.Coalesce)
      {
        existingRegistration!.PendingRequests.Request(request);
        return;
      }

      if (decision != CoalescingWorkDecision.Start)
      {
        return;
      }

      var cancellationToken = stoppingSource!.Token;
      var registration = new MovieProcessingRegistration();
      var processingTask = Task.Run(
          () => ProcessTrackedMovieAsync(
              movieId,
              request,
              registration,
              cancellationToken),
          CancellationToken.None);
      registration.ProcessingTask = processingTask;
      _processingMovies.Add(movieId, registration);
    }
  }

  /// <summary>
  /// Processes a movie and removes its tracked task when complete.
  /// </summary>
  /// <param name="movieId">The stable identifier used to track the movie.</param>
  /// <param name="request">The movie and action to process.</param>
  /// <param name="registration">The movie's active processing registration.</param>
  /// <param name="cancellationToken">A token cancelled when the plugin stops.</param>
  private async Task ProcessTrackedMovieAsync(
      Guid movieId,
      CoalescingWorkRequest<Movie> request,
      MovieProcessingRegistration registration,
      CancellationToken cancellationToken)
  {
    var currentRequest = request;

    while (true)
    {
      try
      {
        await ProcessMovieAsync(
            currentRequest.Item,
            currentRequest.Action,
            cancellationToken).ConfigureAwait(false);
      }
      catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
      {
        _logger.LogDebug(
            "Audio tag processing was cancelled for movie {MovieName}",
            currentRequest.Item.Name);
      }
      catch (Exception ex)
      {
        _logger.LogError(
            ex,
            "Error processing movie {MovieName} for audio tagging",
            currentRequest.Item.Name);
      }

      lock (_syncRoot)
      {
        if (_acceptingEvents
            && !cancellationToken.IsCancellationRequested
            && registration.PendingRequests.TryTake(out var pendingRequest)
            && pendingRequest is not null)
        {
          currentRequest = pendingRequest;
          continue;
        }

        registration.PendingRequests.Clear();
        _processingMovies.Remove(movieId);
        return;
      }
    }
  }

  /// <summary>
  /// Processes a movie for audio tagging.
  /// </summary>
  /// <param name="movie">The movie to process.</param>
  /// <param name="action">The action that triggered this processing (added/updated).</param>
  /// <param name="cancellationToken">A token cancelled when the plugin stops.</param>
  private async Task ProcessMovieAsync(
      Movie movie,
      string action,
      CancellationToken cancellationToken)
  {
    cancellationToken.ThrowIfCancellationRequested();

    var config = Plugin.Instance?.Configuration;
    if (config == null || !config.Enabled)
    {
      return;
    }

    var monitoredLibraries = (config.MonitoredLibraries?.ToArray() ?? Array.Empty<string>())
        .Where(static name => !string.IsNullOrWhiteSpace(name))
        .Select(static name => name.Trim())
        .Distinct(StringComparer.OrdinalIgnoreCase)
        .ToArray();
    var collectionNames = _libraryManager
        .GetCollectionFolders(movie)
        .Select(static folder => folder.Name)
        .Where(static name => !string.IsNullOrWhiteSpace(name))
        .ToArray();

    if (!LibraryMonitorMatcher.IsAnyMonitored(collectionNames, monitoredLibraries))
    {
      if (config.VerboseLogging)
      {
        _logger.LogInformation(
            "Skipping movie {MovieName}. Identified collection folders: {CollectionFolders}. Monitored libraries: {MonitoredLibraries}",
            movie.Name,
            collectionNames.Length == 0 ? "(none)" : string.Join(", ", collectionNames),
            monitoredLibraries.Length == 0
                ? "(none)"
                : string.Join(", ", monitoredLibraries));
      }

      return;
    }

    _logger.LogInformation(
        "Processing {Action} movie: {MovieName} in collection folder(s): {CollectionFolders}",
        action,
        movie.Name,
        string.Join(", ", collectionNames));

    var suggestedTags = _audioAnalysisService.AnalyzeAudioStreams(movie, config);
    cancellationToken.ThrowIfCancellationRequested();

    var reconciliation = TagReconciler.Reconcile(
        movie.Tags,
        suggestedTags,
        AudioStreamTagClassifier.ManagedTags);

    if (!reconciliation.Changed)
    {
      _logger.LogDebug("Audio tags are already current for movie {MovieName}", movie.Name);
      return;
    }

    var originalTags = movie.Tags;
    lock (_syncRoot)
    {
      _selfUpdatingMovies.Add(movie.Id);
    }

    try
    {
      movie.Tags = reconciliation.Tags.ToArray();

      await _libraryManager.UpdateItemAsync(
          movie,
          movie.GetParent(),
          ItemUpdateType.MetadataEdit,
          cancellationToken).ConfigureAwait(false);
    }
    catch
    {
      movie.Tags = originalTags;
      throw;
    }
    finally
    {
      lock (_syncRoot)
      {
        _selfUpdatingMovies.Remove(movie.Id);
      }
    }

    _logger.LogInformation(
        "Reconciled audio tags for movie {MovieName}: {Tags}",
        movie.Name,
        string.Join(", ", reconciliation.Tags));
  }

  private sealed class MovieProcessingRegistration
  {
    internal Task ProcessingTask { get; set; } = Task.CompletedTask;

    internal CoalescingWorkState<CoalescingWorkRequest<Movie>> PendingRequests { get; } = new();
  }

  private static async Task DrainAndDisposeAsync(
      Task[] processingTasks,
      CancellationTokenSource? stoppingSource)
  {
    try
    {
      await Task.WhenAll(processingTasks).ConfigureAwait(false);
    }
    finally
    {
      stoppingSource?.Dispose();
    }
  }
}
