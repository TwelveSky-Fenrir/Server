using Fenrir.Application.Login.Handlers.Dispatching;
using Microsoft.Extensions.DependencyInjection;

namespace Fenrir.Application.Login.Handlers.Extensions;

public static class HandlersServiceCollectionExtensions
{
    public static IServiceCollection AddLoginHandlers(this IServiceCollection services)
    {
        services.AddSingleton<IFrameDispatcher, LoginFrameDispatcher>();

        return services.AddLoginPacketHandlers();
    }
}
