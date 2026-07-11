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

/// <summary>
///     Process-wide Domain singletons: the simulation-system pipeline, cross-zone registries, and
///     <see cref="GameServerOptions" /> binding. Relocated here (unchanged) from Fenrir.GameServer's
///     Program.cs during the project split.
/// </summary>
public static class DomainServiceCollectionExtensions
{
    /// <summary>
    ///     Does NOT bind <see cref="GameServerOptions" /> to configuration itself — that call needs the
    ///     AOT-safe configuration-binding source generator, which is only enabled on FenrirExecutable projects
    ///     (Directory.Build.targets), not this shared class library. The executable's own Program.cs calls
    ///     "services.Configure&lt;GameServerOptions&gt;(configuration.GetSection("Game"))" directly before this method.
    /// </summary>
    public static IServiceCollection AddGameDomain(this IServiceCollection services)
    {
        services.AddSingleton<IValidateOptions<GameServerOptions>, GameServerOptionsValidator>();
        services.AddOptions<GameServerOptions>().ValidateOnStart();

        services.AddSingleton<MovementRules>();
        services.AddSingleton<DirtyTracker<int>>();

        services.AddSingleton<QuestCatalog>();
        services.AddSingleton<KillCooldownTracker>(); // C05 anti-farm gate, shared by every Zone via ZoneRegistry

        // C4 boss/event drop item-id data (BossEventDropResolver's DATA half) -- an immutable Domain-owned static
        // asset built once, injected into MonsterSpawnScheduler above. Registered as the single Default instance so
        // production and any DI-constructed test share the exact same materialized lists.
        services.AddSingleton(BossDropCatalog.Default);

        // C13 War-Point NPC-shop dual-currency price catalogue (legacy WarPointSystem.h) -- the verbatim 48-row
        // WAR_POINT_ITEM_INFO[3][28] table (wave-11 C13-warpoint-prices contract). Registered as the single
        // Production instance so production and any DI-constructed test share the same catalogue; consumed by
        // WarPointShopService.
        services.AddSingleton(WarPointShopCatalog.Production);

        // Registration order IS simulation order within a zone's tick: buffs must expire before meditation regen
        // reads a (possibly just-cleared) sit-skill, and before auto-hunt decides which configured buff is still
        // active; monster AI runs before that tick's respawn scan.

        // FIX_HSB_POS_BUG's anti-camping forced-return check -- registered first (ahead of every other
        // per-avatar system, including BuffExpirySystem below) for the same tick-order-fidelity reason
        // DeathGateTickSystem is registered last: the legacy check runs before any other per-avatar tick work
        // in the same pass (S07_MyGame01.cpp:2031-2038's outer loop). AntiCampingGuardPointCatalog.Empty
        // makes every map a documented no-op until the real per-map coordinate table is supplied -- see that
        // class's own GAP remarks.
        services.AddSingleton(AntiCampingGuardPointCatalog.Empty);
        services.AddSingleton<ISimulationSystem, AntiCampingForcedReturnSystem>();

        services.AddSingleton<ISimulationSystem, BuffExpirySystem>();
        services.AddSingleton<ISimulationSystem, StunCountdownSystem>();
        services.AddSingleton<ISimulationSystem, AutoHuntTickSystem>();
        services.AddSingleton<ISimulationSystem, MeditationRegenSystem>();
        services.AddSingleton<ISimulationSystem, MonsterAiSystem>();
        services.AddSingleton<ISimulationSystem, MonsterSpawnScheduler>();

        // A4 SummonBossMonster 3-state boss-respawn machine (Reload -> Check -> Wait-3h), a separate reserved
        // 20-slot boss window disjoint from MonsterSpawnScheduler's own region-pool spawns (see
        // MonsterBossSpawnSystem's own class remarks for why sourcing it from world.MonsterSpawnRegions would
        // double-spawn). MonsterBossSummonCatalog.Empty makes every map a documented no-op until a verified
        // per-zone boss-id catalog is supplied -- see that catalog's own GAP remarks (the compiled legacy loader
        // reads a .csv the shipped data directory does not contain).
        services.AddSingleton(MonsterBossSummonCatalog.Empty);
        services.AddSingleton<ISimulationSystem, MonsterBossSpawnSystem>();

        // Valley of the Deceased (Zone 200/297/298/299) gate/door/kill-race/boss-window/conclusion broadcast
        // lifecycle -- a distinct legacy state machine from the Zone049 RegularWar family below, see
        // ValleyWarSchedule's own remarks. ValleyWarKillRegistry is registered once here (a plain leaf
        // singleton MonsterSpawnScheduler above also depends on, for its own kill-race quota decrement) so
        // both systems always share the SAME per-map ValleyWarSchedule instance.
        services.AddSingleton<ValleyWarKillRegistry>();
        services.AddSingleton<ISimulationSystem, ValleyWarSystem>();

        services.AddSingleton<ISimulationSystem, TowerGuardianSystem>();
        services.AddSingleton<ISimulationSystem, TowerRewardBonusSystem>();

        // A11 tower construction/heal lifecycle + periodic tower-info push. TowerLifecycleSystem auto-resolves
        // the existing Lazy<ZoneEventBroadcaster> (like TowerGuardianSystem); placed in the tower cluster so a
        // construction-spawned guardian's AI begins the next tick. TowerInfoPushSystem is parameterless and
        // order-independent.
        services.AddSingleton<ISimulationSystem, TowerLifecycleSystem>();
        services.AddSingleton<ISimulationSystem, TowerInfoPushSystem>();
        services.AddSingleton<ISimulationSystem, PetActivitySystem>();
        services.AddSingleton<ISimulationSystem, CashCatalogStaleNotifySystem>();

        // Per-real-minute play-time accrual (PlayTime1-3/PlayTimeEvent) + self-unicast HUD broadcast --
        // self-contained (only reads/writes its own PlayerRuntimeState fields and sends to its own session),
        // so its position relative to every other system here doesn't matter.
        services.AddSingleton<ISimulationSystem, PlayTimeAccrualSystem>();

        // A6: self-throttled per-minute "current time" push (MyUtil::SendTime) -- self-contained, same
        // "order doesn't matter" posture as PlayTimeAccrualSystem above. Registered as its own concrete
        // singleton too (not just via ISimulationSystem) so a future zone-entry/registration handler can
        // inject WorldClockPushSystem directly and call SendForced for the forced (registration) push path
        // -- same dual-registration pattern PopupEventRewardSystem uses further down this same method.
        services.AddSingleton<WorldClockPushSystem>();
        services.AddSingleton<ISimulationSystem>(sp => sp.GetRequiredService<WorldClockPushSystem>());

        // Once-per-real-minute mSupportSkillTimeUpRatio source-field aging/expiry (BuffX2Time countdown,
        // Premium expiry) -- same "self-contained, order doesn't matter" posture as PlayTimeAccrualSystem
        // above: a self-buff cast (AutoHuntTickSystem or Zone.ApplySkillEffectConfirm) that lands in the same
        // tick this recomputes in may read a one-tick-stale ratio, which the behavior contract's own
        // "Staleness" edge case explicitly accepts.
        services.AddSingleton<ISimulationSystem, SupportSkillTimeUpRatioMaintenanceSystem>();

        // Once-per-real-minute Pet EXP boost pill countdown (PetExpX2Time) -- same "self-contained, order
        // doesn't matter" posture as PlayTimeAccrualSystem/SupportSkillTimeUpRatioMaintenanceSystem above.
        services.AddSingleton<ISimulationSystem, PetExpBoostCountdownSystem>();

        // Once-per-real-minute expired-mount auto-dismount (self-contained, defers broadcast via MountAutoDismountPending).
        services.AddSingleton<ISimulationSystem, MountExpiryCountdownSystem>();

        // "Hoisundo" forced-departure countdown for zones 234-240 (once-per-real-minute decrement/broadcast,
        // disconnect below 1) -- self-contained (only reads/writes its own PlayerRuntimeState fields and the
        // zone-wide MapId gate, defers Abort() to after its own player scan same as DeathGateTickSystem
        // below), so its position relative to every other system here doesn't matter.
        services.AddSingleton<ISimulationSystem, HoisundoCountdownSystem>();

        // C21§E: per-tick personal-shop region re-validation -- self-contained (only reads PlayerRuntimeState
        // fields and zone.MapId, defers Abort() to after its own player scan same as HoisundoCountdownSystem
        // above/DeathGateTickSystem below), so its position relative to every other system here doesn't
        // matter.
        services.AddSingleton<ISimulationSystem, PersonalShopRegionEnforcementSystem>();

        // C18: once-per-real-minute timed-buff/scroll/exp-boost countdowns (group-A/B) + paid-zone occupancy
        // eviction (zones 101/125/126/52). Self-contained: sets PlayerRuntimeState.PaidZoneEvictionPending
        // instead of aborting, so its order relative to the other per-minute systems doesn't matter; only the
        // eviction-flag consumer (item 2 of C18's wiring note) must run after it.
        services.AddSingleton<ISimulationSystem, TimedBuffCountdownSystem>();

        // Fishing FishingStep 2->3 "bite window arming" server-driven auto transition, zone-52 only --
        // self-contained (only reads/writes its own PlayerRuntimeState fields and its own MapId gate, and
        // never broadcasts beyond the affected player's own session), so its position relative to every
        // other system here doesn't matter.
        services.AddSingleton<ISimulationSystem, FishingBiteWindowSystem>();

        // Active-duel death/departure/180-tick-timeout resolution (DuelMaintenanceSystem) -- reads
        // IsDead/player-presence state that every combat/movement command already settled during this same
        // tick's DrainInbox stage, so ordering relative to the systems above doesn't matter; kept ahead of
        // DeathGateTickSystem only because that one is deliberately last (see its own comment below).
        services.AddSingleton<ISimulationSystem, DuelMaintenanceSystem>();

        // Regular War (Zone049 active-battle)/Zone195 "Nok-San" AFK enforcement (mAFKTick) -- self-contained
        // (reads its own PlayerRuntimeState.AfkTick field plus the process-wide RegularWarActiveMapTracker/
        // GameServerOptions.Zone195MapIds gates, and only ever disconnects via the same deferred-list pattern
        // as DeathGateTickSystem/HoisundoCountdownSystem), so its position relative to every other system here
        // doesn't matter.
        services.AddSingleton<ISimulationSystem, RegularWarAfkTickSystem>();

        // Kill-streak popup-event reward system (C16) -- event-driven (NotifyPvpKill/NotifyMonsterKill
        // triggers from combat/monster kill resolution), delivers reward on its own next Simulate pass.
        // Self-contained, so its position among the systems above doesn't matter. PopupEventState (the
        // mPopUpTypeState[5] on/off flags) defaults ALL-OFF -> whole system is inert until a flag is armed.
        // NOTE: the NotifyPvpKill/NotifyMonsterKill kill-trigger call sites (Zone.Combat.cs,
        // MonsterSpawnScheduler.cs, Zone ctor field, Zone.PopupEvent.cs) are a deferred follow-up -- the
        // system is registered and inert until those are wired.
        services.AddSingleton<PopupEventState>();
        services.AddSingleton<PopupEventRewardSystem>();
        services.AddSingleton<ISimulationSystem>(sp => sp.GetRequiredService<PopupEventRewardSystem>());

        // A12: the hour/minute timer that actually arms PopupEventState above (Yanggok/Monster/Invasion) --
        // driven by Fenrir.Application.Game.Hosting.Simulation.PopupEventScheduleHost (register that
        // BackgroundService in HostingServiceCollectionExtensions).
        services.AddSingleton<PopupEventScheduleTimer>();

        // "Monster symbol" (mYaoguaiHSB) timer notify -- self-contained (a cheap disabled-flag/map-id check on
        // every zone, real work only on the single zone matching the current holder's mapped instance), so its
        // position relative to every other system here doesn't matter.
        services.AddSingleton<ISimulationSystem, MonsterSymbolAttackWindowNotifySystem>();

        // AdjustSymbolDamageInfo's damage-down half -- unconditional on every zone every tick, same as legacy
        // (see that system's own remarks for why this, unlike the multi-instance RvR world-event schedulers,
        // is a genuine ISimulationSystem rather than a Hosting-driven background service); writes
        // TribeSymbolCombatModifiers, which no other system in this pipeline reads yet, so its position here
        // doesn't matter either.
        services.AddSingleton<ISimulationSystem, TribeSymbolDamageModifierSystem>();

        // Zone175 "Labyrinth" 5-wave PvE mission (workstream A9) -- a per-zone autonomous state machine that
        // self-schedules its Sunday-21:00 open window and drives the wave/reward/terminal lifecycle. Self-
        // contained (reads only the passed Zone, the wall clock, and its own per-zone state; adds no command
        // channel and does not touch the command drain), so its position here doesn't matter. Gated entirely by
        // Zone175LabyrinthConfig: a no-op on every map with the default Disabled catalog.
        services.AddSingleton(Zone175LabyrinthConfig.Disabled);
        services.AddSingleton<ISimulationSystem, Zone175LabyrinthSystem>();

        // Registered last: it can end a session outright (the 50-tick mProtect_ReviveHack force-quit safety
        // valve), so every other system's per-tick mutation for a about-to-be-quit player should already have
        // landed before that happens.
        services.AddSingleton<ISimulationSystem, DeathGateTickSystem>();

        services.AddSingleton<ZoneRegistry>();

        // Domain-owned shared state for "is Regular War (Zone049) currently in its capture/score window on
        // this map" -- written once per tick by Fenrir.Application.Game.Hosting's RegularWarSchedulerHost, read
        // by every Zone via ZoneRegistry to gate the Regular-War-host CP/War Point/Blood Point kill-reward
        // override (Zone.Combat.cs's ApplyRegularWarCpOverride). See RegularWarActiveMapTracker's own remarks
        // for why this small Domain class -- not RegularWarSchedulerHost itself -- is the bridge.
        services.AddSingleton<RegularWarActiveMapTracker>();

        // Ephemeral "has the current monster-symbol holder been notified yet" latch consumed by
        // MonsterSymbolAttackWindowNotifySystem above -- see that class's own remarks.
        services.AddSingleton<MonsterSymbolAttackWindowTracker>();

        // Per-tribe damage-down combat modifier written by TribeSymbolDamageModifierSystem above -- see that
        // class's own remarks for why only this half of AdjustSymbolDamageInfo is modeled.
        services.AddSingleton<TribeSymbolCombatModifiers>();

        // Process-wide singletons: a party/duel/trade/friend-ask/mentor-ask negotiation can span multiple Zone actors
        // within THIS process. PartyRegistry specifically has a documented, still-unresolved cross-shard scope gap
        // when party members are split across two different GameServer shard processes -- see its own class remarks.
        services.AddSingleton<PartyRegistry>();
        services.AddSingleton<FriendRegistry>();
        services.AddSingleton<MentorRegistry>();
        services.AddSingleton<DuelRegistry>();
        services.AddSingleton<TradeRegistry>();
        services.AddSingleton<GuildInviteRegistry>();

        // C21§G: per-tick pending-social-request auto-cancel across all 5 negotiation families above (never
        // Duel -- contract's own Edge cases G notes the asymmetry as an observed, unexplained legacy fact,
        // not something to resolve by guessing). Depends on Lazy<ZoneRegistry>, already registered in
        // Fenrir.Application.Game.Hosting's HostingServiceCollectionExtensions.AddZoneWar (same
        // constructor-graph-cycle-break pattern ValleyWarSystem/Zone195NokSanSystem already use) -- both
        // AddGameDomain and AddGameHosting are called together at GameServer boot, so registration-order
        // between the two extension methods does not matter for DI resolution.
        services.AddSingleton<ISimulationSystem, PendingSocialRequestAutoCancelSystem>();
        services.AddSingleton<TowerWarState>();

        // PvP-kill hero-rank point write-behind (step 8 of the PvP-kill reward pipeline) -- shared across every
        // Zone via ZoneRegistry, flushed periodically by HeroRankPointsWriteBehindHost in Fenrir.Application.Game.Hosting.
        services.AddSingleton<HeroRankPointAccumulator>();

        // C08: RvR ranking-board cache (GuildRankingCache.Top is read synchronously by EnterWorldHandler/
        // ZoneMoveHandler); kept warm by a periodic refresh registered in Fenrir.Application.Game.Hosting.
        services.AddSingleton<GuildRankingCache>();

        // Live cash-shop/blood-exchange catalog cache (CashCatalogStaleNotifySystem above reads it every
        // legacy tick); kept warm by CommerceCatalogRefreshHost, registered in Fenrir.Application.Game.Hosting.
        services.AddSingleton<CommerceCatalogCache>();

        // Bug fix: GameServerOptions itself (not just IOptions<GameServerOptions>) was never resolvable via
        // this container -- ForcedNeutralTribeResetUseItemHandler's own constructor already declared a plain
        // "GameServerOptions options" dependency with nothing registering that concrete type, a latent
        // InvalidOperationException waiting for the first DI resolution of that handler (or, now, this
        // project's second consumer of the same pattern, TribeScrollTransferUseItemHandler). One-time snapshot
        // read at first resolution, same convention HostingServiceCollectionExtensions' own
        // "sp.GetRequiredService<IOptions<GameServerOptions>>().Value" factories already use.
        services.AddSingleton(static provider => provider.GetRequiredService<IOptions<GameServerOptions>>().Value);

        // C11 faction-transfer scroll: boot-time loader for TribeConversionResolver (world.
        // usp_TribeConversionCatalog_GetAll's equivalence data) -- the resolver class existed with a schema,
        // seed data, and a repository read, but no C# consumer or DI wiring anywhere until this. Populated by
        // an explicit Fenrir.GameServer Program.cs boot step, same "empty singleton + deferred factory"
        // pattern as WorldDataLoader/WorldDataCache -- see TribeConversionCatalogLoader's own remarks.
        services.AddSingleton<TribeConversionCatalogLoader>();
        services.AddSingleton(static provider =>
            provider.GetRequiredService<TribeConversionCatalogLoader>().Resolver);

        // C9 op23 per-item use-item dispatch: the Domain-owned registry + its handler family + the shared
        // inventory writer. Injected (optionally) into UseInventoryItemService; without this every C9/C10/C11
        // family (title 891 / palace rank 2193 / registered loot boxes incl. mount-box 635 / double-click-to-
        // equip / forced-neutral tribe reset 8100) falls through to the generic Result=1 failure, exactly its
        // pre-C9 behavior.
        services.AddSingleton<UseItemInventoryWriter>();
        services.AddSingleton<TitleUpgradeUseItemHandler>();
        // C19: title-remove scroll (items 1200/8419/1494) -- the cumulative-CP refund TitleContributionCost
        // already modeled, now wired to a real op23 consumer.
        services.AddSingleton<TitleRemoveScrollUseItemHandler>();
        services.AddSingleton<PalaceRankUpgradeUseItemHandler>();
        services.AddSingleton<EquipSwapUseItemHandler>();
        services.AddSingleton<LootBoxUseItemHandler>();
        // C11: forced-neutral tribe reset (item 8100) -- pure faction flip, no equip/skill/costume remap.
        services.AddSingleton<ForcedNeutralTribeResetUseItemHandler>();
        // C11: faction-transfer scroll (items 8153/8154) -- client-chosen tribe conversion with a 13-gate
        // precondition chain and a best-effort equip/skill remap. Supersedes the old permit-banking stub
        // previously inlined in UseInventoryItemService.ResolveTribeTransferScrollAsync (removed).
        services.AddSingleton<TribeScrollTransferUseItemHandler>();
        // C9-tickets-tower: CP Ticket / Elite Dungeon Ticket / Dungeon Key / Ivy Hall Ticket / Lucky Ticket
        // (stub) / Scroll of Seekers (stub) families.
        services.AddSingleton<CpTicketUseItemHandler>();
        services.AddSingleton<EliteDungeonTicketUseItemHandler>();
        services.AddSingleton<DungeonKeyUseItemHandler>();
        services.AddSingleton<IvyHallTicketUseItemHandler>();
        services.AddSingleton<LuckyTicketUseItemHandler>();
        services.AddSingleton<ScrollOfSeekersUseItemHandler>();
        // C9-costume-stellar-whitelist: the costume/stellar-core wardrobe-grant fallback (tried after the
        // id-keyed dictionary and the equip-swap category match).
        services.AddSingleton<CostumeStellarCoreUseItemHandler>();
        services.AddSingleton<UseItemHandlerRegistry>();

        return services;
    }
}
