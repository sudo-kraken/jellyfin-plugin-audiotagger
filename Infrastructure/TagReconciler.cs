using System;
using System.Collections.Generic;
using System.Linq;

namespace Jellyfin.Plugin.AudioTagger.Infrastructure;

/// <summary>
/// Reconciles tags owned by the plugin while preserving unrelated metadata.
/// </summary>
internal static class TagReconciler
{
  private static readonly StringComparer TagComparer = StringComparer.OrdinalIgnoreCase;

  /// <summary>
  /// Replaces the currently managed tags with the suggested managed tags.
  /// </summary>
  /// <param name="currentTags">The item's current tags.</param>
  /// <param name="suggestedTags">The complete set of managed tags currently suggested for the item.</param>
  /// <param name="managedTags">Every tag owned by the plugin.</param>
  /// <returns>The reconciled tags and whether their effective set changed.</returns>
  internal static TagReconciliationResult Reconcile(
    IEnumerable<string>? currentTags,
    IEnumerable<string>? suggestedTags,
    IEnumerable<string>? managedTags)
  {
    var current = currentTags?.ToArray() ?? Array.Empty<string>();
    var managed = new HashSet<string>(
        managedTags?.Where(static tag => !string.IsNullOrWhiteSpace(tag))
            ?? Enumerable.Empty<string>(),
        TagComparer);
    var suggested = suggestedTags?.Where(
            tag => !string.IsNullOrWhiteSpace(tag) && managed.Contains(tag))
        .Distinct(TagComparer)
        .ToArray()
        ?? Array.Empty<string>();
    var desired = new HashSet<string>(suggested, TagComparer);
    var retainedManagedTags = new HashSet<string>(TagComparer);
    var reconciled = new List<string>(current.Length + suggested.Length);

    foreach (var tag in current)
    {
      if (!managed.Contains(tag))
      {
        reconciled.Add(tag);
      }
      else if (desired.Contains(tag) && retainedManagedTags.Add(tag))
      {
        // Preserve the existing representation and position when the tag is still desired.
        reconciled.Add(tag);
      }
    }

    foreach (var tag in suggested)
    {
      if (retainedManagedTags.Add(tag))
      {
        reconciled.Add(tag);
      }
    }

    var currentEffectiveTags = new HashSet<string>(current, TagComparer);
    var effectiveTagsChanged = !currentEffectiveTags.SetEquals(reconciled);

    return effectiveTagsChanged
        ? new TagReconciliationResult(reconciled.ToArray(), true)
        : new TagReconciliationResult(current, false);
  }
}

/// <summary>
/// Contains the result of reconciling plugin-owned tags.
/// </summary>
internal sealed class TagReconciliationResult
{
  internal TagReconciliationResult(IReadOnlyList<string> tags, bool changed)
  {
    Tags = tags;
    Changed = changed;
  }

  /// <summary>
  /// Gets the reconciled tags.
  /// </summary>
  internal IReadOnlyList<string> Tags { get; }

  /// <summary>
  /// Gets a value indicating whether the effective tag set changed.
  /// </summary>
  internal bool Changed { get; }
}
