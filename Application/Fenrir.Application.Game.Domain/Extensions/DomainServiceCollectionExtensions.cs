using Fenrir.Application.Game.Domain.Combat;
using Fenrir.Application.Game.Domain.Commerce;
using Fenrir.Application.Game.Domain.Guilds;
using Fenrir.Application.Game.Domain.Movement;
using Fenrir.Application.Game.Domain.Progression;
using Fenrir.Application.Game.Domain.Quests;
using Fenrir.Application.Game.Domain.Simulation;
using Fenrir.Application.Game.Domain.Social.Duel;
using Fenrir.Application.Game.Domain.Social.Friends;
using Fenrir.Application.Game.Domain.Social.Mentor;
using Fenrir.Application.Game.Domain.Social.Party;
using Fenrir.Application.Game.Domain.Social.Trade;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Application.Game.Domain.World.Monsters;
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
        services.AddSingleton<ISimulationSystem, TowerGuardianSystem>();
        services.AddSingleton<ISimulationSystem, TowerRewardBonusSystem>();
        services.AddSingleton<ISimulationSystem, PetActivitySystem>();
        services.AddSingleton<ISimulationSystem, CashCatalogStaleNotifySystem>();

        // Per-real-minute play-time accrual (PlayTime1-3/PlayTimeEvent) + self-unicast HUD broadcast --
        // self-contained (only reads/writes its own PlayerRuntimeState fields and sends to its own session),
        // so its position relative to every other system here doesn't matter.
        services.AddSingleton<ISimulationSystem, PlayTimeAccrualSystem>();

        // Once-per-real-minute mSupportSkillTimeUpRatio source-field aging/expiry (BuffX2Time countdown,
        // Premium expiry) -- same "self-contained, order doesn't matter" posture as PlayTimeAccrualSystem
        // above: a self-buff cast (AutoHuntTickSystem or Zone.ApplySkillEffectConfirm) that lands in the same
        // tick this recomputes in may read a one-tick-stale ratio, which the behavior contract's own
        // "Staleness" edge case explicitly accepts.
        services.AddSingleton<ISimulationSystem, SupportSkillTimeUpRatioMaintenanceSystem>();

        // Once-per-real-minute Pet EXP boost pill countdown (PetExpX2Time) -- same "self-contained, order
        // doesn't matter" posture as PlayTimeAccrualSystem/SupportSkillTimeUpRatioMaintenanceSystem above.
        services.AddSingleton<ISimulationSystem, PetExpBoostCountdownSystem>();

        // Active-duel death/departure/180-tick-timeout resolution (DuelMaintenanceSystem) -- reads
        // IsDead/player-presence state that every combat/movement command already settled during this same
        // tick's DrainInbox stage, so ordering relative to the systems above doesn't matter; kept ahead of
        // DeathGateTickSystem only because that one is deliberately last (see its own comment below).
        services.AddSingleton<ISimulationSystem, DuelMaintenanceSystem>();

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

        // Process-wide singletons: a party/duel/trade/friend-ask/mentor-ask negotiation can span multiple Zone actors
        // within THIS process. PartyRegistry specifically has a documented, still-unresolved cross-shard scope gap
        // when party members are split across two different GameServer shard processes -- see its own class remarks.
        services.AddSingleton<PartyRegistry>();
        services.AddSingleton<FriendRegistry>();
        services.AddSingleton<MentorRegistry>();
        services.AddSingleton<DuelRegistry>();
        services.AddSingleton<TradeRegistry>();
        services.AddSingleton<GuildInviteRegistry>();
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

        return services;
    }
}
