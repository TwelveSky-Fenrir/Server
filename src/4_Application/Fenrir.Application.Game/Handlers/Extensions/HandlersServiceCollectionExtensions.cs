using Fenrir.Application.Game.Handlers.Handlers.Dispatching;
using Microsoft.Extensions.DependencyInjection;

namespace Fenrir.Application.Game.Handlers.Extensions;

public static class HandlersServiceCollectionExtensions
{
    public static IServiceCollection AddGameHandlers(this IServiceCollection services)
    {
        services.AddSingleton<IFrameDispatcher, ZoneFrameDispatcher>();

        return services.AddZonePacketHandlers();
    }
}
