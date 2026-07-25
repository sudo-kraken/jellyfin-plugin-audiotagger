using Jellyfin.Plugin.AudioTagger.Infrastructure;
using Jellyfin.Plugin.AudioTagger.Services;
using Xunit;

namespace Jellyfin.Plugin.AudioTagger.Tests;

public class TagReconcilerTests
{
  [Fact]
  public void Reconcile_RemovesStaleManagedTagsAndPreservesUnrelatedTags()
  {
    var result = Reconcile(
        currentTags: ["Favourite", "_Atmos", "_TrueHD", "Family"],
        suggestedTags: ["_DTS", "_Lossy"]);

    Assert.True(result.Changed);
    Assert.Equal(["Favourite", "Family", "_DTS", "_Lossy"], result.Tags);
  }

  [Fact]
  public void Reconcile_RemovesManagedTagsWhenNothingIsSuggested()
  {
    var result = Reconcile(
        currentTags: ["Keep", "_Atmos", "_Lossless"],
        suggestedTags: []);

    Assert.True(result.Changed);
    Assert.Equal(["Keep"], result.Tags);
  }

  [Fact]
  public void Reconcile_AddsMissingManagedTags()
  {
    var result = Reconcile(
        currentTags: ["Keep", "_5.1"],
        suggestedTags: ["_5.1", "_EAC3", "_Lossy"]);

    Assert.True(result.Changed);
    Assert.Equal(["Keep", "_5.1", "_EAC3", "_Lossy"], result.Tags);
  }

  [Fact]
  public void Reconcile_TreatsTagNamesCaseInsensitively()
  {
    var current = new[] { "Keep", "_atmos", "_TRUEHD", "_LOSSLESS" };

    var result = Reconcile(
        currentTags: current,
        suggestedTags: ["_Atmos", "_TrueHD", "_Lossless"]);

    Assert.False(result.Changed);
    Assert.Equal(current, result.Tags);
  }

  [Fact]
  public void Reconcile_DoesNotChangeEffectiveTagsForSuggestedOrderDifferences()
  {
    var current = new[] { "_DTS", "Keep", "_Lossy" };

    var result = Reconcile(
        currentTags: current,
        suggestedTags: ["_Lossy", "_DTS"]);

    Assert.False(result.Changed);
    Assert.Equal(current, result.Tags);
  }

  [Fact]
  public void Reconcile_IgnoresSuggestionsNotOwnedByThePlugin()
  {
    var current = new[] { "Keep" };

    var result = Reconcile(
        currentTags: current,
        suggestedTags: ["Unexpected", "_Atmos"]);

    Assert.True(result.Changed);
    Assert.Equal(["Keep", "_Atmos"], result.Tags);
  }

  [Fact]
  public void Reconcile_UsesCaseInsensitiveManagedTagOwnership()
  {
    var result = TagReconciler.Reconcile(
        currentTags: ["Keep", "_atmos"],
        suggestedTags: [],
        managedTags: ["_Atmos"]);

    Assert.True(result.Changed);
    Assert.Equal(["Keep"], result.Tags);
  }

  private static TagReconciliationResult Reconcile(
      string[] currentTags,
      string[] suggestedTags)
  {
    return TagReconciler.Reconcile(
        currentTags,
        suggestedTags,
        AudioStreamTagClassifier.ManagedTags);
  }
}
