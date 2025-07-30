using System;
using System.Collections.Generic;
using MediaBrowser.Model.Plugins;

namespace Jellyfin.Plugin.AudioTagger.Configuration;

/// <summary>
/// Plugin configuration.
/// </summary>
public class PluginConfiguration : BasePluginConfiguration
{
    /// <summary>
    /// Gets or sets a value indicating whether the plugin is enabled.
    /// </summary>
    public bool Enabled { get; set; } = false;

    /// <summary>
    /// Gets or sets a value indicating whether to only tag 5.1+ audio (skip stereo).
    /// </summary>
    public bool OnlyMultichannelAudio { get; set; } = true;

    /// <summary>
    /// Gets or sets the minimum number of channels to tag.
    /// </summary>
    public int MinimumChannels { get; set; } = 6;

    /// <summary>
    /// Gets or sets the list of library names to monitor.
    /// </summary>
    public List<string> MonitoredLibraries { get; set; } = new List<string> { "Test Movies" };

    /// <summary>
    /// Gets or sets a value indicating whether to log detailed information.
    /// </summary>
    public bool VerboseLogging { get; set; } = true;
}
