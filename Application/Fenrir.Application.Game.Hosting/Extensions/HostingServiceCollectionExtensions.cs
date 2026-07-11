using Fenrir.Application.Game.Domain;
using Fenrir.Application.Game.Domain.Simulation;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Application.Game.Domain.World.Configuration;
using Fenrir.Application.Game.Domain.World.Monsters;
using Fenrir.Application.Game.Domain.World.WorldState;
using Fenrir.Application.Game.Domain.World.ZoneWar;
using Fenrir.Application.Game.Hosting.Commerce;
using Fenrir.Application.Game.Hosting.EventLog;
using Fenrir.Application.Game.Hosting.Guilds;
using Fenrir.Application.Game.Hosting.Progression;
using Fenrir.Application.Game.Hosting.Simulation;
using Fenrir.Application.Game.Hosting.World;
using Fenrir.Application.Game.Hosting.World.Monsters;
using Fenrir.Application.Game.Hosting.World.WorldState;
using Fenrir.Application.Game.Hosting.World.ZoneWar;
using Fenrir.Data.Abstractions.Game;
using Fenrir.Data.Abstractions.Security;
using Fenrir.Data.World;
using Fenrir.Data.WriteBehind;
using Fenrir.Network.Abstractions;
using Fenrir.Network.Dispatch.FloodProtection;
using Fenrir.Network.Dispatch.Sessions;
using Fenrir.Network.Serialization.Wire;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Fenrir.Application.Game.Hosting.Extensions;

/// <summary>
///     Every long-running background process for the GameServer: the write-behind/heartbeat/ticker hosts moved
///     here from Fenrir.Application.Game.Domain, plus the connection listener and boot-order guard moved here
///     from Fenrir.GameServer's Program.cs during the project split.
/// </summary>
public static class HostingServiceCollectionExtensions
{
    public static IServiceCollection AddGameHosting(this IServiceCollection services)
    {
        services.AddSingleton<IOpcodeFrameSizeProvider>(ZoneOpcodeRegistry.Provider);

        // A7: per-zone configuration/data table (level band, owner tribe, MaxUser cap, XP/drop/war ratios),
        // frozen once at boot from GameServerOptions.Zones. Reads only options, so no async preload step is
        // needed -- the singleton is built the first time it's resolved, before the first connection/tick.
        services.AddSingleton(sp =>
            new ZoneConfigCatalog(sp.GetRequiredService<IOptions<GameServerOptions>>().Value.Zones));

        AddWorldState(services);
        AddZoneWar(services);
        AddMonsterBossRespawnTracking(services);

        services.AddHostedService<TowerWarWriteBehindHost>();

        // PvP-kill hero-rank point write-behind (step 8 of the PvP-kill reward pipeline) -- HeroRankPointAccumulator
        // itself is registered in Fenrir.Application.Game.Domain's AddGameDomain.
        services.AddHostedService<HeroRankPointsWriteBehindHost>();

        // C08: guild buff reserve decay (BuffTime counts down over real time -- see GuildBuffDecayHost's remarks
        // for why this is a plain BackgroundService rather than an ISimulationSystem) and the RvR ranking-board
        // cache refresh (seeded once in Program.cs before ZoneConnectionHost starts accepting connections).
        services.AddHostedService<GuildBuffDecayHost>();
        services.AddHostedService<GuildRankingRefreshHost>();

        // guild-buff-expiry: cross-shard fan-out for the guild-buff-reserve-exhaustion immediate strip-effect
        // push -- same "one instance, two registrations" pattern as GuildTribeBroadcastRelayHost below:
        // GuildBuffDecayHost (the sole producer, registered just above) consumes IGuildBuffExpiryRelayQueue only.
        services.AddSingleton<GuildBuffExpiryRelayHost>();
        services.AddSingleton<IGuildBuffExpiryRelayQueue>(sp => sp.GetRequiredService<GuildBuffExpiryRelayHost>());
        services.AddHostedService(sp => sp.GetRequiredService<GuildBuffExpiryRelayHost>());

        // Cash-shop/blood-exchange catalog reload (seeded once in Program.cs before ZoneConnectionHost starts
        // accepting connections, same "initial fill in Program.cs, this host only covers refreshes after
        // that" shape as GuildRankingRefreshHost above).
        services.AddHostedService<CommerceCatalogRefreshHost>();

        services.AddHostedService<ZoneTickHost>();
        services.AddHostedService<MonsterLootFlushHost>();
        services.AddHostedService<ProxyShopExpiryFlushHost>();

        // Death/XP-loss/CP-loss audit trail (Category=Death) -- write-behind twin of MonsterLootFlushHost for
        // Zone.ApplyDeath/Zone.ApplyDeathExperienceLoss's own queued rows (Zone.QueueDeathEventLog).
        services.AddHostedService<DeathEventLogFlushHost>();

        // ProgressWriteBehindHost is a plain singleton, NOT its own BackgroundService/IWriteBehindFlusher --
        // see its own remarks for why a second, independently-timed drain of the SAME shared DirtyTracker<int>
        // would be unsafe. PositionWriteBehindHost is the sole owner of the one write-behind loop/flush signal
        // and calls into it for the Vitals/Progression side of every drained batch.
        services.AddSingleton<ProgressWriteBehindHost>();

        // Same "one instance, several registrations" pattern for a hosted service other code also needs to call
        // directly. ICharacterWriteBehindFlusher is the narrower, character-specific superset of
        // IWriteBehindFlusher that GameConnectionHost's disconnect path needs (FlushCharacterNowAsync) -- see
        // that interface's own remarks for why it isn't just added to IWriteBehindFlusher itself.
        services.AddSingleton<PositionWriteBehindHost>();
        services.AddSingleton<IWriteBehindFlusher>(sp => sp.GetRequiredService<PositionWriteBehindHost>());
        services.AddSingleton<ICharacterWriteBehindFlusher>(sp => sp.GetRequiredService<PositionWriteBehindHost>());
        services.AddHostedService(sp => sp.GetRequiredService<PositionWriteBehindHost>());

        services.AddHostedService<GameServerDirectoryHeartbeat>();
        services.AddHostedService<HeroRankingRolloverHost>();
        services.AddHostedService<GameConnectionHost>();

        // Generic idle-connection reap (Server/ts25zone/S07_MyGame01.cpp:1963-2006's user-liveness loop) --
        // see SessionLivenessSweep's own remarks for why this is a separate, independently-running sweep from
        // TribeQuotaRegistry/TempRegistrationIdleSweep above rather than a replacement for it.
        services.AddSingleton<SessionLivenessSweep>();
        services.AddHostedService<SessionLivenessSweepHost>();

        // Cross-process duplicate-login kick/refusal, Game-side half -- see AccountSessionKickPollHost's remarks.
        services.AddHostedService<AccountSessionKickPollHost>();

        // Bounded-staleness live mute recheck (uppercom-playuser-extra-relay-opcodes) -- see
        // MuteRefreshPollHost's own remarks.
        services.AddHostedService<MuteRefreshPollHost>();

        // Same "one instance, three registrations" pattern as PositionWriteBehindHost above: *.Services
        // producers (enchant/refine/socket/rune per-attempt logging) consume IEventLogQueue only.
        services.AddSingleton<EventLogFlushHost>();
        services.AddSingleton<IEventLogQueue>(sp => sp.GetRequiredService<EventLogFlushHost>());
        services.AddHostedService(sp => sp.GetRequiredService<EventLogFlushHost>());

        // Cross-shard fan-out for GuildAnnouncement/GuildChat/TribeAnnouncement/TribeAnnouncementScroll --
        // same "one instance, three registrations" pattern as EventLogFlushHost above: the four cluster-wide
        // broadcast *.Services producers consume IGuildTribeBroadcastRelayQueue only.
        services.AddSingleton<GuildTribeBroadcastRelayHost>();
        services.AddSingleton<IGuildTribeBroadcastRelayQueue>(sp =>
            sp.GetRequiredService<GuildTribeBroadcastRelayHost>());
        services.AddHostedService(sp => sp.GetRequiredService<GuildTribeBroadcastRelayHost>());

        // WS1.4 cross-shard social-negotiation relay (Party/Friend/Mentor/Duel/Trade/GuildInvite Ask+Answer)
        // -- point-to-point sibling of GuildTribeBroadcastRelayHost above, same "one instance, three
        // registrations" pattern. IEnumerable<ISocialCrossShardRelayHandler> still resolves empty for any
        // negotiation kind whose *.Services registry has not yet registered its own handler (see
        // SocialCrossShardRelayHost's own remarks) -- that is safe, a delivered row for an unregistered Kind
        // is simply logged and dropped.
        services.AddSingleton<SocialCrossShardRelayHost>();
        services.AddSingleton<ISocialCrossShardRelayQueue>(sp =>
            sp.GetRequiredService<SocialCrossShardRelayHost>());
        services.AddHostedService(sp => sp.GetRequiredService<SocialCrossShardRelayHost>());

        // D1 party-resync fan-out, one-instance-three-registrations like SocialCrossShardRelayHost. The
        // *.Services party authority (PartyResyncRelayHandler, Fenrir.Application.Game.Services'
        // AddSocialServices) registers itself as IPartyResyncRelayHandler; a row is only ever logged+dropped
        // here if that registration is absent from a given composition (e.g. a narrower test host) --
        // same-shard party membership is unaffected either way.
        services.AddSingleton<PartyResyncRelayHost>();
        services.AddSingleton<IPartyResyncRelayQueue>(sp => sp.GetRequiredService<PartyResyncRelayHost>());
        services.AddHostedService(sp => sp.GetRequiredService<PartyResyncRelayHost>());

        // Same factory-opacity reasoning as the other Lazy<T> registrations in this file (e.g.
        // Lazy<ZoneEventBroadcaster> below): PartyResyncRelayHandler needs IPartyResyncRelayQueue back (to
        // republish a confirmed reconciliation result), but that interface's own factory just above resolves
        // PartyResyncRelayHost, whose constructor resolves IEnumerable<IPartyResyncRelayHandler> -- a direct
        // constructor dependency on IPartyResyncRelayQueue from the handler deadlocks the container's singleton
        // realization lock (PartyResyncRelayHost's own construction, still in flight, re-entered via the
        // handler it is itself constructing) instead of throwing a clean circular-dependency error, since a
        // factory lambda's own GetRequiredService call is opaque to the static cycle detector. The factory
        // closure only captures the container; it does not resolve IPartyResyncRelayQueue until something
        // actually calls .Value, by which point PartyResyncRelayHost is already constructed and cached.
        services.AddSingleton(sp =>
            new Lazy<IPartyResyncRelayQueue>(sp.GetRequiredService<IPartyResyncRelayQueue>));

        // WS-C3 cross-shard whisper (op39 / SECRET_CHAT) relay -- point-to-point sibling of
        // SocialCrossShardRelayHost above, same "one instance, three registrations" pattern. WhisperService
        // (Fenrir.Application.Game.Services) is the sole producer, consuming IChatCrossShardRelayQueue only;
        // this host delivers each inbound row directly (no per-Kind handler -- whisper is fire-and-forget).
        services.AddSingleton<ChatCrossShardRelayHost>();
        services.AddSingleton<IChatCrossShardRelayQueue>(sp =>
            sp.GetRequiredService<ChatCrossShardRelayHost>());
        services.AddHostedService(sp => sp.GetRequiredService<ChatCrossShardRelayHost>());

        // Registered as a factory (opaque to the DI container's constructor-graph cycle check), same
        // "Lazy<T> breaks the cycle" pattern as Lazy<ZoneEventBroadcaster>/Lazy<ZoneCenterBroadcastIngestor>/
        // Lazy<ZoneRegistry> in AddZoneWar below -- FriendCrossShardRelayHandler/PartyCrossShardRelayHandler
        // (registered as ISocialCrossShardRelayHandler by Fenrir.Application.Game.Services' own
        // AddGameServices) both need to publish a decline back onto ISocialCrossShardRelayQueue, but
        // ISocialCrossShardRelayQueue's own factory just above resolves SocialCrossShardRelayHost, whose own
        // constructor resolves IEnumerable<ISocialCrossShardRelayHandler> -- a direct ISocialCrossShardRelayQueue
        // constructor dependency on either handler would recurse the DI container into resolving
        // SocialCrossShardRelayHost -> handlers -> ISocialCrossShardRelayQueue -> SocialCrossShardRelayHost
        // forever (invisible to the container's static cycle detector, since the factory lambda's own
        // GetRequiredService call is opaque to it) instead of throwing a clean circular-dependency error. The
        // factory closure only captures the container; it does not resolve ISocialCrossShardRelayQueue until
        // something actually calls .Value, by which point SocialCrossShardRelayHost is already constructed
        // and cached.
        services.AddSingleton(sp =>
            new Lazy<ISocialCrossShardRelayQueue>(sp.GetRequiredService<ISocialCrossShardRelayQueue>));

        // proxy-shop-rental-sync: cross-shard fan-out for the proxy/deputy-shop rental-extension consumables'
        // best-effort live-registry mirror update -- single-purpose sibling of GuildTribeBroadcastRelayHost
        // above, same "one instance, three registrations" pattern. UseInventoryItemService consumes
        // IProxyShopExpirationRelayQueue only.
        services.AddSingleton<ProxyShopExpirationRelayHost>();
        services.AddSingleton<IProxyShopExpirationRelayQueue>(sp =>
            sp.GetRequiredService<ProxyShopExpirationRelayHost>());
        services.AddHostedService(sp => sp.GetRequiredService<ProxyShopExpirationRelayHost>());

        services.AddSingleton(sp =>
        {
            var opts = sp.GetRequiredService<IOptions<GameServerOptions>>().Value;
            var firewallRules = sp.GetRequiredService<IFirewallRuleRepository>();
            var registry = sp.GetRequiredService<SessionRegistry>();

            return new IpFloodGuard(
                opts.MaxConnectionsPerIp,
                opts.MaxProtocolViolationsPerIpPerHour,
                firewallRules.BlockAsync,
                registry,
                logger: sp.GetRequiredService<ILogger<IpFloodGuard>>());
        });

        return services;
    }

    /// <summary>
    ///     Same "loader is resolved explicitly at boot" shape as <c>WorldDataServiceCollectionExtensions</c>:
    ///     Program.cs must still call <see cref="WorldStateService.InitializeAsync" /> before accepting
    ///     connections -- registering the singleton here does not load it.
    /// </summary>
    private static void AddWorldState(IServiceCollection services)
    {
        services.AddSingleton<IWorldStateRepository, WorldStateRepository>();
        services.AddSingleton<WorldStateService>();
        services.AddSingleton<WorldStateWriteBehindHost>();
        services.AddHostedService(static provider => provider.GetRequiredService<WorldStateWriteBehindHost>());

        // Tribe bank income tax sweep (1% NPC-service / 9% monster-kill-currency, 10-minute per-zone flush) --
        // TryAddSingleton so a future Commerce/database-engineer workflow can register the real slot-scan/cap-
        // fallback persistent gateway ahead of this call and have it win, without editing this file (same
        // pattern as AddHolyStoneScheduling's own reward/forced-return gateways).
        services.TryAddSingleton<ITribeBankTaxSweepGateway, LoggingOnlyTribeBankTaxSweepGateway>();
        services.AddHostedService<TribeBankTaxSweepFlushHost>();

        // Tribe-point level/rebirth-based recompute + favored-tribe rank-bonus ladder (both share the same
        // ts25center-equivalent every-6th-tick gate) -- TryAddSingleton so a future database-engineer workflow
        // can register the real character/avatar roster-scan gateway ahead of this call and have it win,
        // without editing this file (same pattern as ITribeBankTaxSweepGateway above).
        services.TryAddSingleton<ITribePointRosterGateway, LoggingOnlyTribePointRosterGateway>();
        services.AddSingleton<TribePointLevelRecomputeService>();

        // FavoredTribeRankBonusLadderService now also depends on ZoneEventBroadcaster (tSort=1234 broadcast on
        // a successful flag-triggered recompute) -- registered by AddZoneWar below in this same AddGameHosting
        // call, so plain constructor injection resolves it fine regardless of registration order (DI resolves
        // lazily on first use, not at AddSingleton call time).
        services.AddSingleton<FavoredTribeRankBonusLadderService>();
        services.AddHostedService<TribePointRecomputeHost>();
    }

    /// <summary>
    ///     Same "one instance, resolved directly by tests/tools as well as by the host" shape as
    ///     <see cref="AddWorldState" />.
    /// </summary>
    private static void AddZoneWar(IServiceCollection services)
    {
        services.AddSingleton<TribeVoteElection>();
        services.AddSingleton<ZoneEventBroadcaster>();

        // Center-ingestion counterpart to ZoneEventBroadcaster above: the ~150-way ZONE_BROADCAST_FOR_CENTER_SEND
        // event-code family that class's own remarks say it does NOT cover. ZoneCenterSiegeState is a plain,
        // not-yet-persisted in-memory singleton (same "no backing table yet" posture WorldStateService's own
        // remarks already flag for these exact WorldInfo fields) -- no write-behind host needed for it, see
        // ZoneCenterBroadcastIngestor's own remarks for why. Real API, not yet called by anything (no producer
        // wired up in this cluster), same posture as ZoneWarTickService before anything calls StartWar.
        services.AddSingleton<ZoneCenterSiegeState>();
        // Zone051/Zone053 sibling state -- same "plain, not-yet-persisted in-memory singleton" posture as
        // ZoneCenterSiegeState immediately above, kept in its own type per the A5-zone051-053-states slice's
        // file-collision-avoidance note. ZoneCenterBroadcastIngestor's constructor resolves this as an optional
        // parameter; this registration is what makes the two new selectors-10-30 dispatch cases actually fire
        // instead of silently no-op-ing via their `when` guard.
        services.AddSingleton<Zone051Zone053SiegeState>();
        // Alliance-proposal sibling state (discriminators 46-49, S04_MyWork02.cpp:408-472) -- same posture as
        // Zone051Zone053SiegeState immediately above. ZoneCenterBroadcastIngestor's constructor resolves this
        // as its own optional trailing parameter; this registration is what makes the new 46-49 dispatch case
        // actually fire instead of silently no-op-ing via its `when` guard.
        services.AddSingleton<AllianceProposalCenterState>();
        services.AddSingleton<ZoneCenterBroadcastIngestor>();

        // A12: case-38 Zone039-arming reaction (server-74 monster-summon reset, see
        // IZone039MonsterSummonResetGateway's own remarks for why this defaults to a logging-only
        // placeholder) + case-4000/case-1600 broadcasters.
        services.TryAddSingleton<IZone039MonsterSummonResetGateway, LoggingOnlyZone039MonsterSummonResetGateway>();
        services.AddSingleton<Zone039ArmingReactor>();
        services.AddSingleton<DailyResetBroadcaster>();
        services.AddHostedService<DailyResetBroadcastHost>();
        services.AddHostedService<PopupEventScheduleHost>();

        // Elevated-tier zone-wide FFA-start GM command (tSort 333) process-local countdown/start-trigger
        // bookkeeping -- same "plain in-memory singleton" posture as ZoneCenterSiegeState just above, but
        // deliberately a distinct object from it; see Zone335StartTrigger's own remarks for why. Armed by
        // GmFfaEventStartService (Fenrir.Application.Game.Services), consumed every tick by
        // Zone335FfaEventCycleSystem below (the FFA-335 autonomous tick, legacy Process_Zone_335_FFA).
        services.AddSingleton<Zone335StartTrigger>();

        // FFA-335 autonomous tick state machine -- a per-zone ISimulationSystem (not a separate host/timer:
        // its own MapId==FFAMAPNUM gate at the top of Simulate already makes it a no-op on every shard/zone
        // except the one hosting map 335, see its own remarks). Depends on ZoneEventBroadcaster/
        // ZoneCenterSiegeState/Zone335StartTrigger, all registered above in this same AddZoneWar call.
        services.AddSingleton<ISimulationSystem, Zone335FfaEventCycleSystem>();

        // Registered as a factory (opaque to the DI container's constructor-graph cycle check) so that
        // MonsterSpawnScheduler -- an ISimulationSystem that ZoneRegistry itself resolves at construction
        // time -- can depend on ZoneEventBroadcaster without the container seeing a same-graph cycle back
        // through ZoneEventBroadcaster's own ZoneRegistry dependency. The factory closure only captures
        // the container; it does not resolve ZoneEventBroadcaster until something actually calls .Value,
        // by which point every singleton (including ZoneRegistry) is already constructed and cached.
        services.AddSingleton(sp => new Lazy<ZoneEventBroadcaster>(sp.GetRequiredService<ZoneEventBroadcaster>));

        // Same factory-opacity reasoning as the Lazy<ZoneEventBroadcaster> registration immediately above, for
        // the same underlying reason: RvrSiegeEventRelayHost (registered below) needs ZoneCenterBroadcastIngestor
        // back (to replay a relayed Zone049 row via ApplyRelayedEvent) without a same-container cycle back
        // through ZoneCenterBroadcastIngestor's own IRvrSiegeEventRelayQueue dependency, which resolves to that
        // same host.
        services.AddSingleton(sp =>
            new Lazy<ZoneCenterBroadcastIngestor>(sp.GetRequiredService<ZoneCenterBroadcastIngestor>));

        // Same factory-opacity reasoning as the Lazy<ZoneEventBroadcaster> registration immediately above --
        // ValleyWarSystem is itself one of the ISimulationSystem instances ZoneRegistry resolves at
        // construction time, and needs ZoneRegistry back (to reach a winning-tribe member's reward drop onto
        // whichever OTHER zone currently hosts them, see that class's own remarks) without a same-container cycle.
        services.AddSingleton(sp => new Lazy<ZoneRegistry>(sp.GetRequiredService<ZoneRegistry>));

        // Zone195 "Nok-San" solo point-capture war: process-wide stone state + operator-configured site
        // catalog + the cluster-broadcast seam + the per-zone-tick capture state machine. Same "Lazy<T> breaks
        // the ZoneRegistry constructor-graph cycle" pattern as Lazy<ZoneEventBroadcaster>/ValleyWarSystem above
        // -- Zone195NokSanSystem is itself an ISimulationSystem ZoneRegistry resolves at construction time, and
        // Zone195NokSanBroadcaster needs ZoneRegistry back for the shard-wide op94 fan-out. The site catalog
        // defaults to Empty (feature dormant on every map until an operator supplies real per-map sites -- see
        // DEFERRED options wiring). Zone195NokSanSystem is self-contained (reads its own state + the passed
        // zone's Players, grants via the public Zone.GrantContributionPoints + injected HeroRankPointAccumulator,
        // broadcasts via the seam) and never force-quits a session, so its tick order relative to the other
        // ISimulationSystems does not matter -- registering it here (same as Zone335FfaEventCycleSystem) is fine.
        services.AddSingleton<Zone195NokSanState>();
        services.AddSingleton(Zone195NokSanSiteCatalog.Empty);
        services.AddSingleton<Zone195NokSanBroadcaster>();
        services.AddSingleton<IZone195NokSanBroadcaster>(sp => sp.GetRequiredService<Zone195NokSanBroadcaster>());
        services.AddSingleton(sp =>
            new Lazy<IZone195NokSanBroadcaster>(sp.GetRequiredService<IZone195NokSanBroadcaster>));
        services.AddSingleton<ISimulationSystem, Zone195NokSanSystem>();

        // A5-worldinfo-boot-reset: boot-time regression tripwire over DTM/Nok-San/popup/alliance-possibility's
        // process-wide singletons -- verified once from Program.cs's boot-preload chain (see that file's note).
        // Depends on AllianceProposalCenterState (registered above) and PopupEventState (registered by
        // DomainServiceCollectionExtensions) in addition to ZoneCenterSiegeState/Zone195NokSanState above.
        services.AddSingleton<WorldInfoBootResetVerifier>();

        // rvr-siege: cross-shard fan-out for the Zone049 siege-zone-slot family (sub-codes 1-9, owned by
        // ZoneCenterBroadcastIngestor above) and the tribe-symbol/alliance family (tSort 38/39/40/42/45/46/47,
        // owned by ZoneEventBroadcaster above) -- same "one instance, three registrations" pattern as
        // GuildTribeBroadcastRelayHost. Both producers consume IRvrSiegeEventRelayQueue only, immediately after
        // each one's own unchanged, synchronous same-shard mutation/broadcast; RvrSiegeEventRelayHost's own
        // Lazy<ZoneCenterBroadcastIngestor>/Lazy<ZoneEventBroadcaster> dependencies (registered above) are what
        // let it replay a delivered row straight back through either producer's own logic without a DI cycle.
        services.AddSingleton<RvrSiegeEventRelayHost>();
        services.AddSingleton<IRvrSiegeEventRelayQueue>(sp => sp.GetRequiredService<RvrSiegeEventRelayHost>());
        services.AddHostedService(sp => sp.GetRequiredService<RvrSiegeEventRelayHost>());

        services.AddSingleton<ZoneWarTickService>();
        services.AddHostedService(static provider => provider.GetRequiredService<ZoneWarTickService>());

        // Regular War (Zone049) schedule/capture/victory/reward engine -- UnavailableRegularWarRewardValueProvider
        // is the safe, currently-inert reward default; see its own remarks for why this host is registered and
        // running unconditionally (same posture as ZoneWarTickService above before anything calls StartWar).
        // A5/C15: ZoneCenterRegularWarEventSink (siege-state relay to center) and C15's RegularWarRewardGrantSink
        // (the real money/xp/CP/hero-rank/item-drop reward payout) both need to observe every RegularWarSchedulerHost
        // event, but that host's constructor accepts only one IRegularWarEventSink -- CompositeRegularWarEventSink
        // fans each event out to both, isolating either sink's own failure so one broken sink can't suppress the
        // other. Register each concrete sink on its own type first (ZoneCenterRegularWarEventSink still depends
        // only on ZoneCenterBroadcastIngestor + ILogger; RegularWarRewardGrantSink only on ZoneRegistry + ILogger --
        // no DI cycle either way), then compose.
        services.AddSingleton<ZoneCenterRegularWarEventSink>();
        services.AddSingleton<RegularWarRewardGrantSink>();
        services.AddSingleton<IRegularWarEventSink>(sp => new CompositeRegularWarEventSink(
            [
                sp.GetRequiredService<ZoneCenterRegularWarEventSink>(),
                sp.GetRequiredService<RegularWarRewardGrantSink>()
            ],
            sp.GetRequiredService<ILogger<CompositeRegularWarEventSink>>()));
        services.AddSingleton<IRegularWarRewardValueProvider, UnavailableRegularWarRewardValueProvider>();
        services.AddSingleton<RegularWarSchedulerHost>();
        services.AddHostedService(static provider => provider.GetRequiredService<RegularWarSchedulerHost>());

        // Force-Leader election calendar -- inert everywhere except whichever live shard hosts the
        // designated VoteTribeMapId with VoteTribe enabled; see TribeVoteElectionCalendarHost's own remarks.
        services.AddHostedService<TribeVoteElectionCalendarHost>();

        // TribeGuardSpawner/TribeSymbolSpawner (SummonGuard/SummonTribeSymbol) -- catalogs default to Empty
        // (a documented no-op) until the real per-map/tribe coordinate tables are supplied; see both
        // classes' own remarks. Registered both as their own concrete type (ZoneEventBroadcaster's forced-
        // resummon hooks resolve them directly) and as ISimulationSystem (their own periodic/boot-pass
        // evaluation), same "one instance, two registrations" pattern as PositionWriteBehindHost above.
        services.AddSingleton(GuardPostCatalog.Empty);
        // A4-symbol: TribeSymbolPlacementCatalog.Default replaces the previous inert TribeSymbolCatalog.Empty --
        // populates all four captor rows for every tribe symbol (see TribeSymbolSpawner's own remarks for the
        // real spawn-cadence enablement this activates). Same registered TribeSymbolCatalog type either way.
        services.AddSingleton(TribeSymbolPlacementCatalog.Default);
        services.AddSingleton<TribeGuardOptions>();
        services.AddSingleton<TribeGuardSpawner>();
        services.AddSingleton<ISimulationSystem>(sp => sp.GetRequiredService<TribeGuardSpawner>());
        services.AddSingleton<TribeSymbolSpawner>();
        services.AddSingleton<ISimulationSystem>(sp => sp.GetRequiredService<TribeSymbolSpawner>());

        // Tribe-guard CORRIDOR gate (mTribeGuardState[4][4], MyGame::ProcessForGuardState) -- an entirely
        // different mechanic from TribeGuardSpawner/GuardPostCatalog above (that one is MySummon::SummonGuard's
        // capital-guard spawner); this one only tracks per-checkpoint passability and re-derives it every tick,
        // with no spawning of its own. ZoneMoveService resolves both TribeGuardCorridorCatalog and
        // TribeGuardCorridorState directly (constructor injection) to run WrapCheckSpecialDestinationGate.Evaluate
        // (which itself falls through to TribeGuardCorridorGate.Evaluate for the tribe-corridor group -- see
        // the A8-corridor-respawn note just below) on every zone-transfer request. Registered both as its own
        // concrete type and as ISimulationSystem, same "one instance, two registrations" pattern as
        // TribeGuardSpawner above.
        //
        // CROSS-SHARD SCOPE: TribeGuardCorridorState below is a per-PROCESS singleton (this AddSingleton), not a
        // per-cluster one -- see that class's own remarks for the open, unaddressed gap if the hub zone and the
        // sixteen corridor zones it re-derives/gates are ever split across more than one live shard.
        //
        // A8-corridor-respawn: TribeGuardCorridorCatalogFactory.BuildLive() replaces the Empty placeholder --
        // the real sixteen-corridor-zone/hub chain data is now populated (the per-checkpoint guard-post
        // monster-slot table, TribeGuardCorridorCatalog.TryGetGuardPostSlots, deliberately stays empty -- see
        // that factory's own remarks for the still-open "closes again once guards respawn" gap). ZoneMoveService
        // now dispatches through WrapCheckSpecialDestinationGate (not TribeGuardCorridorGate directly) so the
        // win-zone-038/rebirth-exact/instanced destination groups this catalog's data also feeds are evaluated,
        // not just the tribe-corridor group.
        services.AddSingleton(TribeGuardCorridorCatalogFactory.BuildLive());

        // Portal-proximity hardening catalog (PortalProximityCatalog/PortalProximityGate, contract
        // A8-portal-warzone) -- same "documented always-allow until real per-zone portal coordinate data
        // lands" posture as TribeGuardCorridorCatalog.Empty immediately above. Registered here so it is
        // resolvable the moment ZoneMoveService's constructor gains the corresponding parameter; that
        // constructor change (and the PortalProximityGate.Evaluate call-site insertion in
        // ZoneMoveService.HandleAsync) is Services-layer gameplay wiring, out of this pass's scope -- see
        // this integration pass's own report for why it is deliberately left for fenrir-gameplay-domain-engineer.
        services.AddSingleton(PortalProximityCatalog.Empty);

        services.AddSingleton<TribeGuardCorridorState>();
        services.AddSingleton<TribeGuardCorridorStateDerivationSystem>();
        services.AddSingleton<ISimulationSystem>(sp =>
            sp.GetRequiredService<TribeGuardCorridorStateDerivationSystem>());

        // Alliance diplomacy ceremony: both former GAP 1 (post-occupant scanner) and GAP 2 (negotiation
        // countdown length) are now resolved -- see AllianceDiplomacyCeremony's own remarks and
        // AlliancePostOccupantScanner. Production wiring passes the cited
        // NegotiationConfirmationDurationRawTicks constant for both negotiation-duration arguments (they are
        // identical in legacy, S07_MyGame01.cpp:3904/:3912). AllianceDiplomacyCeremonyHost is the per-tick
        // driver, armed only on whichever live shard hosts GameServerOptions.AllianceTribeMapId with
        // AllianceTribeEnabled set -- see that host's own remarks, same posture as
        // TribeSymbolBattleSchedulerHost/HolyStoneWarCycleHost above.
        services.AddSingleton<AllianceCooldownTracker>();
        services.AddSingleton(sp => new AllianceDiplomacyCeremony(
            sp.GetRequiredService<WorldStateService>(),
            sp.GetRequiredService<AllianceCooldownTracker>(),
            sp.GetRequiredService<ZoneEventBroadcaster>(),
            sp.GetRequiredService<ILogger<AllianceDiplomacyCeremony>>(),
            AllianceDiplomacyCeremony.NegotiationConfirmationDurationRawTicks,
            AllianceDiplomacyCeremony.NegotiationConfirmationDurationRawTicks));
        services.AddHostedService<AllianceDiplomacyCeremonyHost>();

        // TEMP_REGISTER_SEND (op11/ZoneHandshake) tribe-population quota gate: the process-wide directory
        // (TribeQuotaRegistry, also consumed directly by GameConnectionHost's connection-teardown path) plus
        // the idle-timeout sweep that forcibly disconnects an abandoned registration after three minutes.
        services.AddSingleton<TribeQuotaRegistry>();
        services.AddSingleton<TempRegistrationIdleSweep>();
        services.AddHostedService<TempRegistrationIdleSweepHost>();

        AddHolyStoneScheduling(services);
    }

    /// <summary>
    ///     The Zone037/038/039 Holy Stone territory scheduler trio -- see each Domain class's own remarks for
    ///     which numeric/geometry constants the translated behavior contract left unspecified (bound here from
    ///     <see cref="GameServerOptions" />'s own <c>HolyStone*</c> properties, defaulting to inert rather than
    ///     a guessed real value). <c>TryAddSingleton</c> for the reward/forced-return gateways lets a future
    ///     Combat/Commerce/GM-tooling workflow register its own real implementation ahead of this call and have
    ///     it win, without editing this file.
    /// </summary>
    private static void AddHolyStoneScheduling(IServiceCollection services)
    {
        services.TryAddSingleton<IHolyStoneCaptureRewardGateway, LoggingOnlyHolyStoneCaptureRewardGateway>();
        services.TryAddSingleton<IHolyStoneForcedReturnGateway, LoggingOnlyHolyStoneForcedReturnGateway>();

        services.AddSingleton(sp =>
        {
            var opts = sp.GetRequiredService<IOptions<GameServerOptions>>().Value;
            return new TribeSymbolBattleScheduler(
                sp.GetRequiredService<WorldStateService>(),
                sp.GetRequiredService<ZoneEventBroadcaster>(),
                sp.GetRequiredService<ILogger<TribeSymbolBattleScheduler>>(),
                new HashSet<DayOfWeek>(opts.HolyStoneBattleDays),
                opts.HolyStoneTestMode);
        });
        services.AddHostedService<TribeSymbolBattleSchedulerHost>();

        services.AddSingleton(sp =>
        {
            var opts = sp.GetRequiredService<IOptions<GameServerOptions>>().Value;
            var site = new HolyStoneWarSite(opts.HolyStoneMapId, opts.HolyStoneX, opts.HolyStoneZ,
                opts.HolyStoneCaptureRadius, opts.HolyStoneParticipationRadius);

            return new HolyStoneWarCycle(
                sp.GetRequiredService<WorldStateService>(),
                sp.GetRequiredService<ZoneEventBroadcaster>(),
                sp.GetRequiredService<ZoneRegistry>(),
                sp.GetRequiredService<IHolyStoneCaptureRewardGateway>(),
                sp.GetRequiredService<ILogger<HolyStoneWarCycle>>(),
                site,
                testMode: opts.HolyStoneTestMode,
                // A5: wires the Zone038-DTM (holy-stone holder bonus) capture persist through
                // ZoneCenterBroadcastIngestor's code-1510 path -- until supplied, EmitDtmValue no-ops in production.
                siegeIngestor: sp.GetRequiredService<ZoneCenterBroadcastIngestor>());
        });
        services.AddHostedService<HolyStoneWarCycleHost>();

        services.AddSingleton(sp =>
        {
            var opts = sp.GetRequiredService<IOptions<GameServerOptions>>().Value;
            return new HolyStoneTerritoryEvictionSweep(
                sp.GetRequiredService<WorldStateService>(),
                sp.GetRequiredService<ZoneRegistry>(),
                opts.HolyStoneTerritoryMapIds.ToArray(),
                sp.GetRequiredService<IHolyStoneForcedReturnGateway>(),
                sp.GetRequiredService<ILogger<HolyStoneTerritoryEvictionSweep>>());
        });
        services.AddHostedService<HolyStoneTerritoryEvictionSweepHost>();
    }

    /// <summary>
    ///     Same "loader is resolved explicitly at boot" shape as <see cref="AddWorldState" />: Program.cs must
    ///     still call <see cref="MonsterBossRespawnTracker.InitializeAsync" /> before <c>ZoneTickHost</c> starts
    ///     ticking -- registering the singleton here does not load it.
    /// </summary>
    private static void AddMonsterBossRespawnTracking(IServiceCollection services)
    {
        services.AddSingleton<IMonsterBossRespawnTimerRepository, MonsterBossRespawnTimerRepository>();
        services.AddSingleton<MonsterBossRespawnTracker>();
        services.AddHostedService<MonsterBossRespawnWriteBehindHost>();
    }
}
