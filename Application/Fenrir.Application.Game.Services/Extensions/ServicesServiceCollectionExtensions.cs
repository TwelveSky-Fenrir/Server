using Fenrir.Application.Game.Abstractions.BuffsMountsCosmetics;
using Fenrir.Application.Game.Abstractions.Chat;
using Fenrir.Application.Game.Abstractions.Commerce;
using Fenrir.Application.Game.Abstractions.FishingConsumables;
using Fenrir.Application.Game.Abstractions.GenericAction;
using Fenrir.Application.Game.Abstractions.Gm;
using Fenrir.Application.Game.Abstractions.Guilds;
using Fenrir.Application.Game.Abstractions.Hotkeys;
using Fenrir.Application.Game.Abstractions.Inventory;
using Fenrir.Application.Game.Abstractions.ItemModification;
using Fenrir.Application.Game.Abstractions.Progression;
using Fenrir.Application.Game.Abstractions.Quests;
using Fenrir.Application.Game.Abstractions.Social;
using Fenrir.Application.Game.Abstractions.Tribes;
using Fenrir.Application.Game.Abstractions.WarPoint;
using Fenrir.Application.Game.Abstractions.ZoneLifecycle;
using Fenrir.Application.Game.Domain.Forge;
using Fenrir.Application.Game.Domain.Gm;
using Fenrir.Application.Game.Domain.World.WorldState;
using Fenrir.Application.Game.Services.BuffsMountsCosmetics;
using Fenrir.Application.Game.Services.Chat;
using Fenrir.Application.Game.Services.Commerce;
using Fenrir.Application.Game.Services.FishingConsumables;
using Fenrir.Application.Game.Services.GenericAction;
using Fenrir.Application.Game.Services.Gm;
using Fenrir.Application.Game.Services.Guilds;
using Fenrir.Application.Game.Services.Hotkeys;
using Fenrir.Application.Game.Services.Inventory;
using Fenrir.Application.Game.Services.ItemModification;
using Fenrir.Application.Game.Services.Progression;
using Fenrir.Application.Game.Services.Quests;
using Fenrir.Application.Game.Services.Social;
using Fenrir.Application.Game.Services.Tribes;
using Fenrir.Application.Game.Services.WarPoint;
using Fenrir.Application.Game.Services.ZoneLifecycle;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Fenrir.Application.Game.Services.Extensions;

public static class ServicesServiceCollectionExtensions
{
    public static IServiceCollection AddGameServices(this IServiceCollection services)
    {
        services.TryAddSingleton(TimeProvider.System);

        AddGuildsServices(services);
        AddTribeServices(services);
        AddQuestsServices(services);
        AddGenericActionServices(services);
        AddChatServices(services);
        AddCommerceServices(services);
        AddProgressionServices(services);
        AddSocialServices(services);
        AddItemModificationServices(services);
        AddFishingConsumablesServices(services);
        AddBuffsMountsCosmeticsServices(services);
        AddZoneLifecycleServices(services);
        AddGmServices(services);
        AddInventoryServices(services);

        services.AddSingleton<ITribePointRosterGateway, SqlTribePointRosterGateway>();
        services.AddSingleton<ITribeBankTaxSweepGateway, SqlTribeBankTaxSweepGateway>();
        services.AddSingleton<FourGuildScoringService>();
        services.AddSingleton<TribeBankWithdrawService>();

        services.AddSingleton<FourGuildKillPointRelayHost>();
        services.AddSingleton<IFourGuildKillPointQueue>(sp => sp.GetRequiredService<FourGuildKillPointRelayHost>());
        services.AddHostedService(sp => sp.GetRequiredService<FourGuildKillPointRelayHost>());
        services.AddHostedService<FourGuildScoringRecomputeHost>();

        return services;
    }

    private static void AddGuildsServices(IServiceCollection services)
    {
        services.AddSingleton<IGuildActionService, GuildActionService>();
    }

    private static void AddTribeServices(IServiceCollection services)
    {
        services.AddSingleton<ITribeActionService, TribeActionService>();
        services.AddSingleton<ITribeAnnouncementScrollService, TribeAnnouncementScrollService>();
        services.AddSingleton<ITribeBankService, TribeBankService>();
        services.AddSingleton<ITribePopulationService, TribePopulationService>();
        services.AddSingleton<ITribeVoteService, TribeVoteService>();
        services.AddSingleton<ITribeMigrationService, TribeMigrationService>();
    }

    private static void AddQuestsServices(IServiceCollection services)
    {
        services.AddSingleton<IQuestProgressService, QuestProgressService>();
    }

    private static void AddGenericActionServices(IServiceCollection services)
    {
        services.AddSingleton<IGenericActionService, GenericActionService>();

        services.AddSingleton<IWarPointShopService, WarPointShopService>();

        services.AddSingleton<IHotkeyActionService, HotkeyActionService>();
        services.AddSingleton<IPetBagActionService, PetBagActionService>();
    }

    private static void AddChatServices(IServiceCollection services)
    {
        services.AddSingleton<IGlobalAnnouncementService, GlobalAnnouncementService>();
        services.AddSingleton<IGuildAnnouncementService, GuildAnnouncementService>();
        services.AddSingleton<IGuildChatService, GuildChatService>();
        services.AddSingleton<ILocalChatService, LocalChatService>();
        services.AddSingleton<YangGokPvpDropEventState>();
        services.AddSingleton<IPartyChatService, PartyChatService>();
        services.AddSingleton<IShoutService, ShoutService>();
        services.AddSingleton<ITribeAnnouncementService, TribeAnnouncementService>();
        services.AddSingleton<ITribeChatService, TribeChatService>();
        services.AddSingleton<IWhisperService, WhisperService>();
        services.AddSingleton<IWorldChatService, WorldChatService>();
        services.AddSingleton<IWorldNoticeService, WorldNoticeService>();
    }

    private static void AddCommerceServices(IServiceCollection services)
    {
        services.AddSingleton<IBuyBloodMarkItemService, BuyBloodMarkItemService>();
        services.AddSingleton<IBuyCashItemService, BuyCashItemService>();
        services.AddSingleton<IBuyShopItemService, BuyShopItemService>();
        services.AddSingleton<IClaimDailyRewardService, ClaimDailyRewardService>();
        services.AddSingleton<ICloseShopStallService, CloseShopStallService>();
        services.AddSingleton<IGetBloodMarkCatalogService, GetBloodMarkCatalogService>();
        services.AddSingleton<IGetCashBalanceService, GetCashBalanceService>();
        services.AddSingleton<IGetCashCatalogService, GetCashCatalogService>();
        services.AddSingleton<IGetDailyRewardCatalogService, GetDailyRewardCatalogService>();
        services.AddSingleton<IGetProxyShopService, GetProxyShopService>();
        services.AddSingleton<IOpenShopStallService, OpenShopStallService>();
        services.AddSingleton<ISearchShopListingsService, SearchShopListingsService>();
        services.AddSingleton<IUpdateProxyShopService, UpdateProxyShopService>();
        services.AddSingleton<IViewShopStallService, ViewShopStallService>();
        services.AddSingleton<IWithdrawProxyShopEarningsService, WithdrawProxyShopEarningsService>();
    }

    private static void AddProgressionServices(IServiceCollection services)
    {
        services.AddSingleton<IAutoHuntToggleService, AutoHuntToggleService>();
        services.AddSingleton<IAutoPotionThresholdService, AutoPotionThresholdService>();
        services.AddSingleton<IDailyMissionService, DailyMissionService>();
        services.AddSingleton<IHeroRankingService, HeroRankingService>();
        services.AddSingleton<IHeroRewardClaimService, HeroRewardClaimService>();
        services.AddSingleton<ITowerUpgradeService, TowerUpgradeService>();
    }

    private static void AddSocialServices(IServiceCollection services)
    {
        services.AddSingleton<IDuelService, DuelService>();
        services.AddSingleton<IFriendService, FriendService>();
        services.AddSingleton<IGuildInviteService, GuildInviteService>();
        services.AddSingleton<IFindGuildMemberService, FindGuildMemberService>();
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

        services.AddSingleton<ISocialCrossShardRelayHandler, FriendCrossShardRelayHandler>();
        services.AddSingleton<ISocialCrossShardRelayHandler, PartyCrossShardRelayHandler>();
        services.AddSingleton<ISocialCrossShardRelayHandler, GuildCrossShardRelayHandler>();
        services.AddSingleton<ISocialCrossShardRelayHandler, TradeCrossShardRelayHandler>();

        services.AddSingleton<IPartyResyncRelayHandler, PartyResyncRelayHandler>();
    }

    private static void AddItemModificationServices(IServiceCollection services)
    {
        services.AddSingleton<ICombineItemService, CombineItemService>();
        services.AddSingleton<ICraftItemService, CraftItemService>();
        services.AddSingleton<ICraftLegendaryPetService, CraftLegendaryPetService>();
        services.AddSingleton<ICraftPetService, CraftPetService>();
        services.AddSingleton<ICraftSkillBookService, CraftSkillBookService>();
        services.AddSingleton<IDestroyItemService, DestroyItemService>();
        services.AddSingleton<IDowngradeItemRankService, DowngradeItemRankService>();
        services.AddSingleton<IEnchantItemService, EnchantItemService>();
        services.AddSingleton<IRerollItemService, RerollItemService>();
        services.AddSingleton<IRuneSocketService, RuneSocketService>();
        services.AddSingleton<IRuneStoneCraftService, RuneStoneCraftService>();
        services.AddSingleton<ISkyUpgradeItemService, SkyUpgradeItemService>();
        services.AddSingleton<IUpgradeCapeService, UpgradeCapeService>();
        services.AddSingleton<WarlordPityLockState>();
        services.AddSingleton<IUpgradeItemRankService, UpgradeItemRankService>();
    }

    private static void AddFishingConsumablesServices(IServiceCollection services)
    {
        services.AddSingleton<IFishingCatchService, FishingCatchService>();
        services.AddSingleton<IFishingLineService, FishingLineService>();
        services.AddSingleton<IFishingProgressService, FishingProgressService>();
        services.AddSingleton<IDrinkBottleService, DrinkBottleService>();
    }

    private static void AddBuffsMountsCosmeticsServices(IServiceCollection services)
    {
        services.AddSingleton<IPlaytimeBuffService, PlaytimeBuffService>();
        services.AddSingleton<IRankBuffService, RankBuffService>();
        services.AddSingleton<IMountAbsorbService, MountAbsorbService>();
        services.AddSingleton<IMountStateService, MountStateService>();
        services.AddSingleton<IPetActionUpdateService, PetActionUpdateService>();
        services.AddSingleton<ICostumeStateService, CostumeStateService>();
        services.AddSingleton<ICostumeVisibilityService, CostumeVisibilityService>();
        services.AddSingleton<IStellarCoreStateService, StellarCoreStateService>();
    }

    private static void AddZoneLifecycleServices(IServiceCollection services)
    {
        services.AddSingleton<IAvatarActionService, AvatarActionService>();
        services.AddSingleton<IContinueSkillStatService, ContinueSkillStatService>();
        services.AddSingleton<IContinueSkillUseService, ContinueSkillUseService>();
        services.AddSingleton<IEnterWorldService, EnterWorldService>();
        services.AddSingleton<IHeartbeatService, HeartbeatService>();
        services.AddSingleton<IUseHotkeyItemService, UseHotkeyItemService>();
        services.AddSingleton<IUseInventoryItemService, UseInventoryItemService>();
        services.AddSingleton<IZoneHandshakeService, ZoneHandshakeService>();
        services.AddSingleton<IZoneMoveService, ZoneMoveService>();
        services.AddSingleton<IZoneReadyService, ZoneReadyService>();
        services.AddSingleton<IAttackService, AttackService>();
    }

    private static void AddGmServices(IServiceCollection services)
    {
        services.AddSingleton<IGmBlockAvatarService, GmBlockAvatarService>();
        services.AddSingleton<IGmCreateItemService, GmCreateItemService>();
        services.AddSingleton<IGmMaxStatService, GmMaxStatService>();
        services.AddSingleton<IGmPetExperienceGrantService, GmPetExperienceGrantService>();

        services.AddSingleton<IGmExpGrantService, GmExpGrantService>();
        services.AddSingleton<IGmGrantMoneyService, GmGrantMoneyService>();
        services.AddSingleton<IGmFfaEventStartService, GmFfaEventStartService>();
        services.AddSingleton<IGmSummonMonsterService, GmSummonMonsterService>();

        services.AddSingleton<IGmBasicCommandService, GmBasicCommandService>();

        services.AddSingleton<IGmSetPvpPointService, GmSetPvpPointService>();
        services.AddSingleton<IGmCallPvpService, GmCallPvpService>();
        services.AddSingleton<IGmClearInventoryService, GmClearInventoryService>();
    }

    private static void AddInventoryServices(IServiceCollection services)
    {
        services.AddSingleton<IInventoryToWorldDropService, InventoryToWorldDropService>();

        services.AddSingleton<IBigMoneyTransferService, BigMoneyTransferService>();
    }
}
