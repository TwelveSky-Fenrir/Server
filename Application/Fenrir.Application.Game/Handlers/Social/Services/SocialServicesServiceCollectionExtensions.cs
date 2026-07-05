using Microsoft.Extensions.DependencyInjection;

namespace Fenrir.Application.Game.Handlers.Social.Services;

/// <summary>
///     Registers the orchestration services extracted out of the Social (mentor/party/trade) packet
///     handlers. Singleton, same lifetime reasoning as the registries/repositories they wrap: these are
///     stateless orchestrators.
/// </summary>
public static class SocialServicesServiceCollectionExtensions
{
    public static IServiceCollection AddSocialServices(this IServiceCollection services)
    {
        services.AddSingleton<IMentorAnswerService, MentorAnswerService>();
        services.AddSingleton<IMentorAskService, MentorAskService>();
        services.AddSingleton<IMentorCancelService, MentorCancelService>();
        services.AddSingleton<IMentorEndService, MentorEndService>();
        services.AddSingleton<IMentorStartService, MentorStartService>();
        services.AddSingleton<IMentorStatusService, MentorStatusService>();

        services.AddSingleton<IPartyAnswerService, PartyAnswerService>();
        services.AddSingleton<IPartyCancelService, PartyCancelService>();
        services.AddSingleton<IPartyDisbandService, PartyDisbandService>();
        services.AddSingleton<IPartyInviteService, PartyInviteService>();
        services.AddSingleton<IPartyKickService, PartyKickService>();
        services.AddSingleton<IPartyLeaveService, PartyLeaveService>();

        services.AddSingleton<ITradeAnswerService, TradeAnswerService>();
        services.AddSingleton<ITradeCancelService, TradeCancelService>();
        services.AddSingleton<ITradeEndService, TradeEndService>();
        services.AddSingleton<ITradeInviteService, TradeInviteService>();
        services.AddSingleton<ITradeLockService, TradeLockService>();
        services.AddSingleton<ITradeStartService, TradeStartService>();

        return services;
    }
}
