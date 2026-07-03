using Fenrir.Application.Login.RateLimiting;
using Fenrir.Contracts.Dispatch;
using Microsoft.Extensions.DependencyInjection;

namespace Fenrir.Application.Login;

/// <summary>
///     DI registration entry point for every <c>IInlinePacketHandler{T}</c>/<c>IAsyncPacketHandler{T}</c> declared
///     in this project (via the GENERATED <see cref="GeneratedHandlerRegistration.AddGeneratedPacketHandlers" /> --
///     same discovery pass as the dispatch table, so a dispatchable-but-unregistered handler cannot exist), plus
///     the collaborators they need that no other assembly's registration extension already covers.
/// </summary>
/// <remarks>
///     Deliberately does not register <see cref="Fenrir.Data.Accounts.AccountRepository" />,
///     <see cref="Fenrir.Data.Characters.CharacterRepository" />,
///     <see cref="Fenrir.Data.Runtime.SessionTicketRepository" />,
///     <see cref="Fenrir.Data.Runtime.GameServerDirectoryRepository" />
///     (all four come from <c>Fenrir.Data</c>'s own <c>AddFenrirData()</c>), nor
///     <see cref="Fenrir.Network.Sessions.SessionRegistry" /> or <c>IOptions&lt;LoginServerOptions&gt;</c>
///     (both registered directly in Fenrir.LoginServer's <c>Program.cs</c>) -- registering any of those a second
///     time here would risk a duplicate/shadowing singleton instead of the single shared one every handler expects.
/// </remarks>
public static class LoginHandlersServiceCollectionExtensions
{
    public static IServiceCollection AddLoginHandlers(this IServiceCollection services)
    {
        // Not a handler itself, but has no other registration site yet (LoginHandler is its only consumer today).
        services.AddSingleton<LoginIpRateLimiter>();

        return services.AddGeneratedPacketHandlers();
    }
}
