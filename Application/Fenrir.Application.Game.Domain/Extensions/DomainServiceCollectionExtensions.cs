using Fenrir.Application.Game.Domain.Combat;
using Fenrir.Application.Game.Domain.Commerce;
using Fenrir.Application.Game.Domain.Guilds;
using Fenrir.Application.Game.Domain.Inventory.UseItems;
using Fenrir.Application.Game.Domain.Inventory.UseItems.Boxes;
using Fenrir.Application.Game.Domain.Movement;
using Fenrir.Application.Game.Domain.Progression;
using Fenrir.Application.Game.Domain.Quests;
using Fenrir.Application.Game.Domain.Simulation;
using Fenrir.Application.Game.Domain.Social.Duel;
using Fenrir.Application.Game.Domain.Social.Friends;
using Fenrir.Application.Game.Domain.Social.Mentor;
using Fenrir.Application.Game.Domain.Social.Party;
using Fenrir.Application.Game.Domain.Social.Trade;
using Fenrir.Application.Game.Domain.Tribes;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Application.Game.Domain.World.Loot;
using Fenrir.Application.Game.Domain.World.Monsters;
using Fenrir.Application.Game.Domain.World.Npcs;
using Fenrir.Application.Game.Domain.World.ZoneWar;
using Fenrir.Data.WriteBehind;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Fenrir.Application.Game.Domain.Extensions;

public static class DomainServiceCollectionExtensions
{

        public static IServiceCollection AddGameDomain(this IServiceCollection services)
    {
        services.AddSingleton<IValidateOptions<GameServerOptions>, GameServerOptionsValidator>();
        services.AddOptions<GameServerOptions>().ValidateOnStart();

        services.AddSingleton<MovementRules>();
        services.AddSingleton<DirtyTracker<int>>();

        services.AddSingleton<QuestCatalog>();
        services.AddSingleton<KillCooldownTracker>();

        services.AddSingleton(BossDropCatalog.Default);

        services.AddSingleton(WarPointShopCatalog.Production);


        services.AddSingleton(AntiCampingGuardPointCatalog.Default);
        services.AddSingleton<ISimulationSystem, AntiCampingForcedReturnSystem>();

        services.AddSingleton<ISimulationSystem, BuffExpirySystem>();
        services.AddSingleton<ISimulationSystem, StunCountdownSystem>();
        services.AddSingleton<ISimulationSystem, AutoHuntTickSystem>();
        services.AddSingleton<ISimulationSystem, MeditationRegenSystem>();
        services.AddSingleton<ISimulationSystem, MonsterAiSystem>();
        services.AddSingleton<ISimulationSystem, MonsterSpawnScheduler>();

        services.AddSingleton(MonsterBossSummonCatalog.Empty);
        services.AddSingleton<ISimulationSystem, MonsterBossSpawnSystem>();

        services.AddSingleton<ValleyWarKillRegistry>();
        services.AddSingleton<ISimulationSystem, ValleyWarSystem>();

        services.AddSingleton<ISimulationSystem, TowerGuardianSystem>();
        services.AddSingleton<ISimulationSystem, TowerRewardBonusSystem>();

        services.AddSingleton<ISimulationSystem, TowerLifecycleSystem>();
        services.AddSingleton<ISimulationSystem, TowerInfoPushSystem>();
        services.AddSingleton<ISimulationSystem, PetActivitySystem>();
        services.AddSingleton<ISimulationSystem, CashCatalogStaleNotifySystem>();

        services.AddSingleton<ISimulationSystem, PlayTimeAccrualSystem>();

        services.AddSingleton<WorldClockPushSystem>();
        services.AddSingleton<ISimulationSystem>(sp => sp.GetRequiredService<WorldClockPushSystem>());

        services.AddSingleton<ISimulationSystem, SupportSkillTimeUpRatioMaintenanceSystem>();

        services.AddSingleton<ISimulationSystem, PetExpBoostCountdownSystem>();

        services.AddSingleton<ISimulationSystem, MountExpiryCountdownSystem>();

        services.AddSingleton<ISimulationSystem, HoisundoCountdownSystem>();

        services.AddSingleton<ISimulationSystem, PersonalShopRegionEnforcementSystem>();

        services.AddSingleton<ISimulationSystem, TimedBuffCountdownSystem>();

        services.AddSingleton<ISimulationSystem, FishingBiteWindowSystem>();

        services.AddSingleton<ISimulationSystem, DuelMaintenanceSystem>();

        services.AddSingleton<ISimulationSystem, RegularWarAfkTickSystem>();

        services.AddSingleton<PopupEventState>();
        services.AddSingleton<PopupEventRewardSystem>();
        services.AddSingleton<ISimulationSystem>(sp => sp.GetRequiredService<PopupEventRewardSystem>());

        services.AddSingleton<PopupEventScheduleTimer>();

        services.AddSingleton<ISimulationSystem, MonsterSymbolAttackWindowNotifySystem>();

        services.AddSingleton<ISimulationSystem, TribeSymbolDamageModifierSystem>();

        services.AddSingleton(Zone175LabyrinthConfig.Disabled);
        services.AddSingleton<ISimulationSystem, Zone175LabyrinthSystem>();

        services.AddSingleton<ISimulationSystem, DeathGateTickSystem>();

        services.AddSingleton<ZoneRegistry>();

        services.AddSingleton<RegularWarActiveMapTracker>();

        services.AddSingleton<MonsterSymbolAttackWindowTracker>();

        services.AddSingleton<TribeSymbolCombatModifiers>();

        services.AddSingleton<PartyRegistry>();
        services.AddSingleton<FriendRegistry>();
        services.AddSingleton<MentorRegistry>();
        services.AddSingleton<DuelRegistry>();
        services.AddSingleton<TradeRegistry>();
        services.AddSingleton<GuildInviteRegistry>();

        services.AddSingleton<ISimulationSystem, PendingSocialRequestAutoCancelSystem>();
        services.AddSingleton<TowerWarState>();

        services.AddSingleton<HeroRankPointAccumulator>();

        services.AddSingleton<GuildRankingCache>();

        services.AddSingleton<CommerceCatalogCache>();

        services.AddSingleton(static provider => provider.GetRequiredService<IOptions<GameServerOptions>>().Value);

        services.AddSingleton<TribeConversionCatalogLoader>();
        services.AddSingleton(static provider =>
            provider.GetRequiredService<TribeConversionCatalogLoader>().Resolver);

        services.AddSingleton<UseItemInventoryWriter>();
        services.AddSingleton<TitleUpgradeUseItemHandler>();
        services.AddSingleton<TitleRemoveScrollUseItemHandler>();
        services.AddSingleton<PalaceRankUpgradeUseItemHandler>();
        services.AddSingleton<EquipSwapUseItemHandler>();
        services.AddSingleton<LootBoxUseItemHandler>();
        services.AddSingleton<ForcedNeutralTribeResetUseItemHandler>();
        services.AddSingleton<TribeScrollTransferUseItemHandler>();
        services.AddSingleton<CpTicketUseItemHandler>();
        services.AddSingleton<EliteDungeonTicketUseItemHandler>();
        services.AddSingleton<DungeonKeyUseItemHandler>();
        services.AddSingleton<IvyHallTicketUseItemHandler>();
        services.AddSingleton<LuckyTicketUseItemHandler>();
        services.AddSingleton<ScrollOfSeekersUseItemHandler>();
        services.AddSingleton<CostumeStellarCoreUseItemHandler>();
        services.AddSingleton<UseItemHandlerRegistry>();

        return services;
    }
}
