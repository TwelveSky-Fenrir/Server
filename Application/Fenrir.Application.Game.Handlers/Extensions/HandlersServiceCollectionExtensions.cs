using Fenrir.Application.Game.Handlers.Handlers.Dispatching;
using Fenrir.Network.Abstractions;
using Microsoft.Extensions.DependencyInjection;

namespace Fenrir.Application.Game.Handlers.Extensions;

/// <summary>
///     Registers the zone frame dispatcher plus every packet handler via
///     <see cref="GeneratedHandlerRegistration.AddGeneratedPacketHandlers" />, so one is never missed and left
///     unconstructible.
/// </summary>
public static class HandlersServiceCollectionExtensions
{
    public static IServiceCollection AddGameHandlers(this IServiceCollection services)
    {
        services.AddSingleton<IFrameDispatcher, ZoneFrameDispatcher>();

        return services.AddGeneratedPacketHandlers();
    }
}
