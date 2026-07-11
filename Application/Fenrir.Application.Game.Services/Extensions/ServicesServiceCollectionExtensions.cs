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

/// <summary>Registers every Handler-delegate business-logic Service extracted from the packet handlers.</summary>
public static class ServicesServiceCollectionExtensions
{
    public static IServiceCollection AddGameServices(this IServiceCollection services)
    {
        // TryAdd: harmless if some other registration (e.g. a test host) already supplied one -- this is the
        // only consumer today (TribeMigrationService's own wall-clock read), not a general-purpose clock
        // abstraction rollout.
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

        // C17: real SQL-backed gateways replace the LoggingOnly placeholders. Registered here (in
        // AddGameServices, which runs before AddGameHosting) so HostingServiceCollectionExtensions.AddWorldState's
        // TryAddSingleton<...LoggingOnly...> no-ops and these win -- the mechanism its own comments describe.
        services.AddSingleton<ITribePointRosterGateway, SqlTribePointRosterGateway>();
        services.AddSingleton<ITribeBankTaxSweepGateway, SqlTribeBankTaxSweepGateway>();
        services.AddSingleton<FourGuildScoringService>();
        services.AddSingleton<TribeBankWithdrawService>();

        // C17 Part B1/B2 wiring: FourGuildKillPointRelayHost bridges Zone's own tick thread (Domain, cannot
        // depend on this project) into FourGuildScoringService.AccrueKillPointAsync -- same "one instance,
        // three registrations" shape as Hosting.EventLog.EventLogFlushHost, but rooted here (not in
        // Fenrir.Application.Game.Hosting) since that project has no reference to this one. See
        // FourGuildKillPointRelayHost's own remarks for why. FourGuildScoringRecomputeHost is the separate
        // ~10-second leaderboard-recompute driver (Part B2); both are unconditionally started, matching every
        // other always-on write-behind/relay host in this codebase.
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

        // C13 War-Point (WP/CP dual-currency) NPC-shop purchase. Registered here alongside GenericActionService
        // because the WP path is offered ahead of the ordinary NpcShopPolicy buy inside BuyFromNpcShopAsync.
        // GenericActionService's constructor takes this as an optional (test-seam) parameter, resolved live here
        // via DI -- BuyFromNpcShopAsync now actually calls TryBuyAsync before falling through to NpcShopPolicy.
        services.AddSingleton<IWarPointShopService, WarPointShopService>();

        // C8-hotkey-money-petbag: hotkey-bind-family (tSort 204/205/211/214/216/253) and pet-bag-family
        // (tSort 254/255/256) business logic behind GenericActionHandler's dispatch. Registered here even
        // though GenericActionHandler's own dispatch branches for these sorts are not yet wired (Services
        // layer's own report flags this as a Handlers-layer, packet-dispatch edit out of this pass's scope) --
        // a "wired-ready but currently unused" posture, unlike IWarPointShopService immediately above which is
        // now actually consumed.
        services.AddSingleton<IHotkeyActionService, HotkeyActionService>();
        services.AddSingleton<IPetBagActionService, PetBagActionService>();
    }

    private static void AddChatServices(IServiceCollection services)
    {
        services.AddSingleton<IGlobalAnnouncementService, GlobalAnnouncementService>();
        services.AddSingleton<IGuildAnnouncementService, GuildAnnouncementService>();
        services.AddSingleton<IGuildChatService, GuildChatService>();
        services.AddSingleton<ILocalChatService, LocalChatService>();
        // A14: shard-wide YangGok PvP-drop-event toggle state read/written by LocalChatService's `ygdrop`
        // GM command (its first ctor dependency). Domain-owned singleton; the drop-roll consumer is a
        // separate, not-yet-modeled subsystem.
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

        // WS1.4: the cross-shard social-negotiation relay's own target-shard-delivery/asker-shard-completion
        // handlers -- Party and Friend are the full reference implementation (see each type's own remarks);
        // Mentor/Duel/Trade/GuildInvite deliberately have none registered yet (ask-publish-only for now, see
        // MentorAskService/DuelService/TradeInviteService/GuildInviteService's own remarks). Consumed as
        // IEnumerable<ISocialCrossShardRelayHandler> by Fenrir.Application.Game.Hosting's own
        // SocialCrossShardRelayHost.
        services.AddSingleton<ISocialCrossShardRelayHandler, FriendCrossShardRelayHandler>();
        services.AddSingleton<ISocialCrossShardRelayHandler, PartyCrossShardRelayHandler>();
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

        // Elevated tier (GmCommandTier.Elevated): grant-experience-to-self (tSort 503), grant-money (tSort 504,
        // legacy dead code -- see IGmGrantMoneyService's own remarks), zone-wide FFA-start (tSort 333),
        // summon-monster/"moncall" (tSort 506).
        services.AddSingleton<IGmExpGrantService, GmExpGrantService>();
        services.AddSingleton<IGmGrantMoneyService, GmGrantMoneyService>();
        services.AddSingleton<IGmFfaEventStartService, GmFfaEventStartService>();
        services.AddSingleton<IGmSummonMonsterService, GmSummonMonsterService>();

        // Basic tier (GmCommandTier.Basic): HIDE/SHOW (501/502), self-teleport-to-coordinate MOVE (507), DIE
        // (508), TRIBE (510), EQUIP/UNEQUIP (511/512), FIND (513), CALL (514), self-teleport-to-target MOVE
        // (515), NCHAT/YCHAT (516/517), KICK (518), TRIBEBANK (520, dead code), LEVEL (521), STR/DEX/VIT/INT-edit
        // (522, dead code) -- see IGmBasicCommandService's own remarks.
        services.AddSingleton<IGmBasicCommandService, GmBasicCommandService>();

        // Basic tier (GmCommandTier.Basic), A14-gm-remaining: GM-SETPVPPOINT (tSort 598, confirmed functional
        // no-op once validated), GM-CALLPVP (tSort 599, relocates a matched connected character -- placeholder
        // 0,0,0 coordinates until cpp-zone-gameplay-analyst supplies the real S04_MyWork04.cpp:1770-1823
        // triples, see GmCallPvpService's own remarks), GM_CLEAR_INVENTORY (tSort 701, wipes the invoking GM's
        // own inventory page(s)).
        services.AddSingleton<IGmSetPvpPointService, GmSetPvpPointService>();
        services.AddSingleton<IGmCallPvpService, GmCallPvpService>();
        services.AddSingleton<IGmClearInventoryService, GmClearInventoryService>();
    }

    /// <summary>
    ///     tSort 209 (CZ_PROCESS_DATA_SEND, "drop item from inventory to world") -- not yet reachable from
    ///     GenericActionHandler's own dispatch switch; wiring that up is a separate integration step, see
    ///     IInventoryToWorldDropService's own remarks.
    /// </summary>
    private static void AddInventoryServices(IServiceCollection services)
    {
        services.AddSingleton<IInventoryToWorldDropService, InventoryToWorldDropService>();

        // C8 BigMoney ("1B") family (tSort 241/242/244/245, Inventory<->Store/Save) -- see
        // Fenrir.Data.ServiceCollectionExtensions' own IBigMoneyRepository registration remarks for the
        // Characters-vs-Inventory-namespace collision this consumes the resolution of. Not yet reachable from
        // GenericActionHandler's own dispatch switch; wiring that up is a separate integration step, same
        // posture as IInventoryToWorldDropService immediately above.
        services.AddSingleton<IBigMoneyTransferService, BigMoneyTransferService>();
    }
}
