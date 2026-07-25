using System;
using System.Collections.Generic;

namespace Jellyfin.Plugin.AudioTagger.Infrastructure;

/// <summary>
/// Matches the collection folders that contain an item against the configured libraries.
/// </summary>
internal static class LibraryMonitorMatcher
{
  /// <summary>
  /// Determines whether at least one identified collection folder is configured for monitoring.
  /// </summary>
  /// <param name="collectionNames">The names of the collection folders that contain the item.</param>
  /// <param name="monitoredLibraries">The configured collection folder names.</param>
  /// <returns><see langword="true"/> when an identified collection is monitored; otherwise, <see langword="false"/>.</returns>
  internal static bool IsAnyMonitored(
    IEnumerable<string?>? collectionNames,
    IEnumerable<string?>? monitoredLibraries)
  {
    if (collectionNames is null || monitoredLibraries is null)
    {
      return false;
    }

    var monitoredNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    foreach (var monitoredLibrary in monitoredLibraries)
    {
      if (!string.IsNullOrWhiteSpace(monitoredLibrary))
      {
        monitoredNames.Add(monitoredLibrary.Trim());
      }
    }

    if (monitoredNames.Count == 0)
    {
      return false;
    }

    foreach (var collectionName in collectionNames)
    {
      if (!string.IsNullOrWhiteSpace(collectionName)
          && monitoredNames.Contains(collectionName.Trim()))
      {
        return true;
      }
    }

    return false;
  }
}
