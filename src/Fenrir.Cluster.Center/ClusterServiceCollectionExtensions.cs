using Fenrir.Cluster.Center.Wire;
using Microsoft.Extensions.DependencyInjection;

namespace Fenrir.Cluster.Center;

public static class ClusterServiceCollectionExtensions
{
    public static IServiceCollection AddFenrirCluster(this IServiceCollection services)
    {

        services.AddSingleton<IFrameDispatcher, CenterFrameDispatcher>();
        services.AddCenterPacketHandlers();

        return services;
    }
}
