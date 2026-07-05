using Fenrir.Application.Login.Handlers.Dispatching;
using Fenrir.Network.Abstractions;
using Fenrir.Network.Dispatch;
using Microsoft.Extensions.DependencyInjection;

namespace Fenrir.Application.Login.Handlers.Extensions;

/// <summary>
///     Registers the login frame dispatcher plus every packet handler via
///     <see cref="GeneratedHandlerRegistration.AddGeneratedPacketHandlers" />.
/// </summary>
public static class HandlersServiceCollectionExtensions
{
    public static IServiceCollection AddLoginHandlers(this IServiceCollection services)
    {
        services.AddSingleton<IFrameDispatcher, LoginFrameDispatcher>();

        return services.AddGeneratedPacketHandlers();
    }
}
