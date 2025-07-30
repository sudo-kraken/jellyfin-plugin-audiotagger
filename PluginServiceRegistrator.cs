using Jellyfin.Plugin.AudioTagger.Services;
using MediaBrowser.Controller;
using MediaBrowser.Controller.Plugins;
using Microsoft.Extensions.DependencyInjection;

namespace Jellyfin.Plugin.AudioTagger;

/// <summary>
/// Plugin service registrar.
/// </summary>
public class PluginServiceRegistrator : IPluginServiceRegistrator
{
    /// <inheritdoc />
    public void RegisterServices(IServiceCollection serviceCollection, IServerApplicationHost applicationHost)
    {
        serviceCollection.AddSingleton<AudioAnalysisService>();
        serviceCollection.AddHostedService<ServerEntryPoint>();
    }
}
