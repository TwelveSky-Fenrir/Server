using Fenrir.Contracts.Dispatch;
using Microsoft.Extensions.DependencyInjection;

namespace Fenrir.Application.Game;

/// <summary>
///     Registers every zone packet handler via <see cref="GeneratedHandlerRegistration.AddGeneratedPacketHandlers" />
///     so one is never missed and left unconstructible.
/// </summary>
public static class GameHandlersServiceCollectionExtensions
{
    public static IServiceCollection AddGameHandlers(this IServiceCollection services)
    {
        return services.AddGeneratedPacketHandlers();
    }
}
