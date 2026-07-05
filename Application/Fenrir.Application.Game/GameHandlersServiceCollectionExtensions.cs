using Fenrir.Application.Game.Handlers.Chat.Services;
using Fenrir.Application.Game.Handlers.Commerce.Services;
using Fenrir.Application.Game.Handlers.GenericAction.Services;
using Fenrir.Application.Game.Handlers.Guilds.Services;
using Fenrir.Application.Game.Handlers.ItemModification.Services;
using Fenrir.Application.Game.Handlers.Progression.Services;
using Fenrir.Application.Game.Handlers.Quests.Services;
using Fenrir.Application.Game.Handlers.Social.Services;
using Fenrir.Application.Game.Handlers.Tribes.Services;
using Fenrir.Application.Game.Handlers.FishingConsumables.Services;
using Fenrir.Application.Game.Handlers.BuffsMountsCosmetics.Services;
using Fenrir.Application.Game.ZoneLifecycle.Services;
using Fenrir.Network.Dispatch;
using Microsoft.Extensions.DependencyInjection;

namespace Fenrir.Application.Game;

/// <summary>
///     Registers every zone packet handler via <see cref="GeneratedHandlerRegistration.AddGeneratedPacketHandlers" />
///     so one is never missed and left unconstructible, plus the business-logic Services extracted out of them.
/// </summary>
public static class GameHandlersServiceCollectionExtensions
{
    public static IServiceCollection AddGameHandlers(this IServiceCollection services)
    {
        services.AddGuildsServices();
        services.AddTribeServices();
        services.AddQuestsServices();
        services.AddGenericActionServices();
        services.AddChatServices();
        services.AddCommerceServices();
        services.AddProgressionServices();
        services.AddSocialServices();
        services.AddItemModificationServices();
        services.AddFishingConsumablesServices();
        services.AddBuffsMountsCosmeticsServices();
        services.AddZoneLifecycleServices();

        return services.AddGeneratedPacketHandlers();
    }
}
