using Fenrir.Application.Login.Handlers.Services;
using Fenrir.Application.Login.RateLimiting;
using Fenrir.Network.Dispatch;
using Microsoft.Extensions.DependencyInjection;

namespace Fenrir.Application.Login;

/// <summary>Registers this project's generated packet handlers plus collaborators not already registered elsewhere.</summary>
/// <remarks>
///     Does not re-register Data-layer repositories or SessionRegistry/LoginServerOptions (registered in Program.cs)
///     — avoid duplicate singletons.
/// </remarks>
public static class LoginHandlersServiceCollectionExtensions
{
    public static IServiceCollection AddLoginHandlers(this IServiceCollection services)
    {
        services.AddSingleton<LoginIpRateLimiter>();
        services.AddLoginServices();

        return services.AddGeneratedPacketHandlers();
    }
}
