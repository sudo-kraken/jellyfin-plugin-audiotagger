namespace Jellyfin.Plugin.AudioTagger.Infrastructure;

/// <summary>
/// Describes how an incoming work request should be scheduled.
/// </summary>
internal enum CoalescingWorkDecision
{
  Ignore,
  Start,
  Coalesce,
}

/// <summary>
/// Makes the pure scheduling decision for an incoming coalesced work request.
/// </summary>
internal static class CoalescingWorkScheduler
{
  /// <summary>
  /// Decides whether to ignore, start, or coalesce an incoming request.
  /// </summary>
  /// <param name="canAcceptWork">Whether the lifecycle currently accepts new work.</param>
  /// <param name="hasActiveRegistration">Whether the item already has active work.</param>
  /// <param name="isExpectedSelfUpdate">Whether this request appears to be a self-update.</param>
  /// <returns>The scheduling decision.</returns>
  internal static CoalescingWorkDecision Decide(
      bool canAcceptWork,
      bool hasActiveRegistration,
      bool isExpectedSelfUpdate)
  {
    if (!canAcceptWork)
    {
      return CoalescingWorkDecision.Ignore;
    }

    if (hasActiveRegistration)
    {
      return CoalescingWorkDecision.Coalesce;
    }

    return isExpectedSelfUpdate
        ? CoalescingWorkDecision.Ignore
        : CoalescingWorkDecision.Start;
  }
}

/// <summary>
/// Couples an item with the action that requested work for it.
/// </summary>
/// <typeparam name="T">The item type.</typeparam>
/// <param name="Item">The item to process.</param>
/// <param name="Action">The action that requested processing.</param>
internal sealed record CoalescingWorkRequest<T>(T Item, string Action);

/// <summary>
/// Stores at most one pending work request, replacing it with the most recent request.
/// </summary>
/// <typeparam name="T">The work request type.</typeparam>
internal sealed class CoalescingWorkState<T>
{
  private T? _pendingWork;

  /// <summary>
  /// Gets a value indicating whether work is pending.
  /// </summary>
  internal bool HasPendingWork { get; private set; }

  /// <summary>
  /// Records a pending work request, replacing any request already waiting.
  /// </summary>
  /// <param name="work">The work request.</param>
  internal void Request(T work)
  {
    _pendingWork = work;
    HasPendingWork = true;
  }

  /// <summary>
  /// Takes the pending work request, if one exists.
  /// </summary>
  /// <param name="work">The pending request.</param>
  /// <returns><see langword="true"/> when pending work was returned; otherwise, <see langword="false"/>.</returns>
  internal bool TryTake(out T? work)
  {
    if (!HasPendingWork)
    {
      work = default;
      return false;
    }

    work = _pendingWork;
    _pendingWork = default;
    HasPendingWork = false;
    return true;
  }

  /// <summary>
  /// Removes any pending work.
  /// </summary>
  internal void Clear()
  {
    _pendingWork = default;
    HasPendingWork = false;
  }
}
