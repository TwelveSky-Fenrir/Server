using Fenrir.Contracts.Dispatch;
using Microsoft.Extensions.DependencyInjection;

namespace Fenrir.Application.Game;

/// <summary>
///     Registers every zone packet handler as a DI singleton via the GENERATED
///     <see cref="GeneratedHandlerRegistration.AddGeneratedPacketHandlers" /> (same discovery pass as the
///     dispatch table itself), so a handler reachable from <c>MessageDispatcher</c> is always constructible —
///     the old hand-maintained AddSingleton list could silently miss one and crash on the first packet.
/// </summary>
public static class GameHandlersServiceCollectionExtensions
{
    public static IServiceCollection AddGameHandlers(this IServiceCollection services)
    {
        return services.AddGeneratedPacketHandlers();
    }
}
