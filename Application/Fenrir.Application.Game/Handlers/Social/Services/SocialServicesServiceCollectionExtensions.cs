using Microsoft.Extensions.DependencyInjection;

namespace Fenrir.Application.Game.Handlers.Social.Services;

/// <summary>
///     Registers the Social handler batch's extracted services (Duel/Friend/GuildInvite/FindGuildMember) --
///     stateless orchestrators over the existing process-wide registries, same singleton lifetime reasoning
///     as those registries themselves.
/// </summary>
public static class SocialServicesServiceCollectionExtensions
{
    public static IServiceCollection AddSocialServices(this IServiceCollection services)
    {
        services.AddSingleton<IDuelService, DuelService>();
        services.AddSingleton<IFriendService, FriendService>();
        services.AddSingleton<IGuildInviteService, GuildInviteService>();
        services.AddSingleton<IFindGuildMemberService, FindGuildMemberService>();

        return services;
    }
}
