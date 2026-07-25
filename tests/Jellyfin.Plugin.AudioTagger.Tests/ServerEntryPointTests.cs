using Jellyfin.Plugin.AudioTagger.Infrastructure;
using Xunit;

namespace Jellyfin.Plugin.AudioTagger.Tests;

public class ServerEntryPointTests
{
  [Fact]
  public void CoalescingWorkScheduler_CoalescesSelfUpdateWhenWorkIsActive()
  {
    var decision = CoalescingWorkScheduler.Decide(
        canAcceptWork: true,
        hasActiveRegistration: true,
        isExpectedSelfUpdate: true);

    Assert.Equal(CoalescingWorkDecision.Coalesce, decision);
  }

  [Fact]
  public void CoalescingWorkScheduler_IgnoresSelfUpdateWithoutActiveWork()
  {
    var decision = CoalescingWorkScheduler.Decide(
        canAcceptWork: true,
        hasActiveRegistration: false,
        isExpectedSelfUpdate: true);

    Assert.Equal(CoalescingWorkDecision.Ignore, decision);
  }

  [Fact]
  public void CoalescingWorkState_KeepsOnlyTheMostRecentPendingRequest()
  {
    var state = new CoalescingWorkState<string>();

    state.Request("added");
    state.Request("updated");

    Assert.True(state.TryTake(out var action));
    Assert.Equal("updated", action);
    Assert.False(state.TryTake(out _));
  }

  [Fact]
  public void CoalescingWorkState_KeepsTheLatestItemAndActionTogether()
  {
    var state = new CoalescingWorkState<CoalescingWorkRequest<object>>();
    var firstItem = new object();
    var latestItem = new object();

    state.Request(new CoalescingWorkRequest<object>(firstItem, "added"));
    state.Request(new CoalescingWorkRequest<object>(latestItem, "updated"));

    Assert.True(state.TryTake(out var request));
    Assert.NotNull(request);
    Assert.Same(latestItem, request.Item);
    Assert.Equal("updated", request.Action);
  }

  [Fact]
  public void CoalescingWorkState_ClearDiscardsPendingWork()
  {
    var state = new CoalescingWorkState<string>();
    state.Request("updated");

    state.Clear();

    Assert.False(state.HasPendingWork);
    Assert.False(state.TryTake(out _));
  }

  [Fact]
  public void LibraryMonitorMatcher_DefaultsToDenyWhenNoCollectionIsIdentified()
  {
    var monitored = LibraryMonitorMatcher.IsAnyMonitored(
        collectionNames: [],
        monitoredLibraries: ["Movies"]);

    Assert.False(monitored);
  }

  [Fact]
  public void LibraryMonitorMatcher_DefaultsToDenyWhenNoLibraryIsConfigured()
  {
    var monitored = LibraryMonitorMatcher.IsAnyMonitored(
        collectionNames: ["Movies"],
        monitoredLibraries: []);

    Assert.False(monitored);
  }

  [Fact]
  public void LibraryMonitorMatcher_DefaultsToDenyForNullInputs()
  {
    Assert.False(LibraryMonitorMatcher.IsAnyMonitored(null, ["Movies"]));
    Assert.False(LibraryMonitorMatcher.IsAnyMonitored(["Movies"], null));
  }

  [Fact]
  public void LibraryMonitorMatcher_TrimsAndMatchesNamesCaseInsensitively()
  {
    var monitored = LibraryMonitorMatcher.IsAnyMonitored(
        collectionNames: ["  MOVIES  "],
        monitoredLibraries: [" movies "]);

    Assert.True(monitored);
  }

  [Fact]
  public void LibraryMonitorMatcher_MatchesAnyContainingCollection()
  {
    var monitored = LibraryMonitorMatcher.IsAnyMonitored(
        collectionNames: ["Kids", "Films"],
        monitoredLibraries: ["Movies", " Films "]);

    Assert.True(monitored);
  }

  [Fact]
  public void LibraryMonitorMatcher_IgnoresBlankNames()
  {
    var monitored = LibraryMonitorMatcher.IsAnyMonitored(
        collectionNames: [null, "", "   "],
        monitoredLibraries: [null, "", "Movies"]);

    Assert.False(monitored);
  }
}
