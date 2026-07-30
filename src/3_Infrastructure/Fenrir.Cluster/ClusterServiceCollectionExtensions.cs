using Fenrir.Cluster.Directory;
using Fenrir.Cluster.Wire;
using Microsoft.Extensions.DependencyInjection;

namespace Fenrir.Cluster;

public static class ClusterServiceCollectionExtensions
{
    public static IServiceCollection AddFenrirCluster(this IServiceCollection services)
    {
        services.AddSingleton<IZoneDirectory, ZoneDirectory>();

        services.AddSingleton<IFrameDispatcher, CenterFrameDispatcher>();
        services.AddCenterPacketHandlers();

        return services;
    }
}
