using Fenrir.Application.Game.Abstractions.World;
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
using Fenrir.Data.WriteBehind;
using Fenrir.Domain.Game.GameData;
using Fenrir.Network.Dispatch.FloodProtection;
using Fenrir.Network.Dispatch.Sessions;
using Fenrir.Protocol.Game;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Fenrir.Application.Game.Hosting.Extensions;

public static class HostingServiceCollectionExtensions
{
    public static IServiceCollection AddGameHosting(this IServiceCollection services)
    {
        services.AddSingleton<IOpcodeFrameSizeProvider>(ZoneOpcodeRegistry.Provider);

        services.AddSingleton(sp =>
            new ZoneConfigCatalog(sp.GetRequiredService<IOptions<GameServerOptions>>().Value.Zones));

        AddWorldState(services);
        AddZoneWar(services);
        AddMonsterBossRespawnTracking(services);

        services.AddHostedService<TowerWarWriteBehindHost>();

        services.AddHostedService<HeroRankPointsWriteBehindHost>();

        services.AddHostedService<GuildBuffDecayHost>();
        services.AddHostedService<GuildRankingRefreshHost>();

        services.AddSingleton<GuildBuffExpiryRelayHost>();
        services.AddSingleton<IGuildBuffExpiryRelayQueue>(sp => sp.GetRequiredService<GuildBuffExpiryRelayHost>());
        services.AddHostedService(sp => sp.GetRequiredService<GuildBuffExpiryRelayHost>());

        services.AddHostedService<CommerceCatalogRefreshHost>();

        services.AddHostedService<ProxyShopBootReloadHost>();

        services.AddSingleton<EventLogFlushHost>();
        services.AddSingleton<IEventLogQueue>(sp => sp.GetRequiredService<EventLogFlushHost>());
        services.AddHostedService(sp => sp.GetRequiredService<EventLogFlushHost>());

        services.AddHostedService<MonsterLootFlushHost>();
        services.AddHostedService<ProxyShopExpiryFlushHost>();
        services.AddHostedService<DeathEventLogFlushHost>();

        services.AddHostedService<ZoneTickHost>();

        services.AddSingleton<ProgressWriteBehindHost>();

        services.AddSingleton<PositionWriteBehindHost>();
        services.AddSingleton<IWriteBehindFlusher>(sp => sp.GetRequiredService<PositionWriteBehindHost>());
        services.AddSingleton<ICharacterWriteBehindFlusher>(sp => sp.GetRequiredService<PositionWriteBehindHost>());
        services.AddHostedService(sp => sp.GetRequiredService<PositionWriteBehindHost>());

        services.AddHostedService<GameServerDirectoryHeartbeat>();
        services.AddHostedService<HeroRankingRolloverHost>();
        services.AddHostedService<GameConnectionHost>();

        services.AddSingleton<SessionLivenessSweep>();
        services.AddHostedService<SessionLivenessSweepHost>();

        services.AddHostedService<AccountSessionKickPollHost>();

        services.AddHostedService<MuteRefreshPollHost>();

        services.AddSingleton<GuildTribeBroadcastRelayHost>();
        services.AddSingleton<IGuildTribeBroadcastRelayQueue>(sp =>
            sp.GetRequiredService<GuildTribeBroadcastRelayHost>());
        services.AddHostedService(sp => sp.GetRequiredService<GuildTribeBroadcastRelayHost>());

        services.AddSingleton<SocialCrossShardRelayHost>();
        services.AddSingleton<ISocialCrossShardRelayQueue>(sp =>
            sp.GetRequiredService<SocialCrossShardRelayHost>());
        services.AddHostedService(sp => sp.GetRequiredService<SocialCrossShardRelayHost>());

        services.AddSingleton<PartyResyncRelayHost>();
        services.AddSingleton<IPartyResyncRelayQueue>(sp => sp.GetRequiredService<PartyResyncRelayHost>());
        services.AddHostedService(sp => sp.GetRequiredService<PartyResyncRelayHost>());

        services.AddSingleton(sp =>
            new Lazy<IPartyResyncRelayQueue>(sp.GetRequiredService<IPartyResyncRelayQueue>));

        services.AddSingleton<ChatCrossShardRelayHost>();
        services.AddSingleton<IChatCrossShardRelayQueue>(sp =>
            sp.GetRequiredService<ChatCrossShardRelayHost>());
        services.AddHostedService(sp => sp.GetRequiredService<ChatCrossShardRelayHost>());

        services.AddSingleton(sp =>
            new Lazy<ISocialCrossShardRelayQueue>(sp.GetRequiredService<ISocialCrossShardRelayQueue>));

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

    private static void AddWorldState(IServiceCollection services)
    {
        services.AddSingleton<WorldStateService>();
        services.AddSingleton<WorldStateWriteBehindHost>();
        services.AddHostedService(static provider => provider.GetRequiredService<WorldStateWriteBehindHost>());

        services.TryAddSingleton<ITribeBankTaxSweepGateway, LoggingOnlyTribeBankTaxSweepGateway>();
        services.AddHostedService<TribeBankTaxSweepFlushHost>();

        services.TryAddSingleton<ITribePointRosterGateway, LoggingOnlyTribePointRosterGateway>();
        services.AddSingleton<TribePointLevelRecomputeService>();

        services.AddSingleton<FavoredTribeRankBonusLadderService>();
        services.AddHostedService<TribePointRecomputeHost>();
    }

    private static void AddZoneWar(IServiceCollection services)
    {
        services.AddSingleton<TribeVoteElection>();
        services.AddSingleton<IWorldEventUplink, WorldEventUplink>();
        services.AddSingleton<ZoneEventBroadcaster>();

        services.AddSingleton<ZoneCenterSiegeState>();
        services.AddSingleton<Zone051Zone053SiegeState>();
        services.AddSingleton<AllianceProposalCenterState>();
        services.AddSingleton<ZoneCenterBroadcastIngestor>();


        services.TryAddSingleton<IZone039MonsterSummonResetGateway, Zone039MonsterSummonResetGateway>();
        services.AddSingleton<Zone039ArmingReactor>();
        services.AddSingleton<DailyResetBroadcaster>();
        services.AddHostedService<DailyResetBroadcastHost>();
        services.AddHostedService<PopupEventScheduleHost>();

        services.AddSingleton<Zone335StartTrigger>();

        services.AddSingleton<ISimulationSystem, Zone335FfaEventCycleSystem>();

        services.AddSingleton(sp => new Lazy<ZoneEventBroadcaster>(sp.GetRequiredService<ZoneEventBroadcaster>));

        services.AddSingleton(sp =>
            new Lazy<ZoneCenterBroadcastIngestor>(sp.GetRequiredService<ZoneCenterBroadcastIngestor>));

        services.AddSingleton(sp => new Lazy<ZoneRegistry>(sp.GetRequiredService<ZoneRegistry>));

        services.AddSingleton<Zone195NokSanState>();
        services.AddSingleton(Zone195NokSanSiteCatalog.Legacy);
        services.AddSingleton<Zone195NokSanBroadcaster>();
        services.AddSingleton<IZone195NokSanBroadcaster>(sp => sp.GetRequiredService<Zone195NokSanBroadcaster>());
        services.AddSingleton(sp =>
            new Lazy<IZone195NokSanBroadcaster>(sp.GetRequiredService<IZone195NokSanBroadcaster>));
        services.AddSingleton<ISimulationSystem, Zone195NokSanSystem>();

        services.AddSingleton<WorldInfoBootResetVerifier>();

        services.AddSingleton<RvrSiegeEventRelayHost>();
        services.AddSingleton<IRvrSiegeEventRelayQueue>(sp => sp.GetRequiredService<RvrSiegeEventRelayHost>());
        services.AddHostedService(sp => sp.GetRequiredService<RvrSiegeEventRelayHost>());

        services.AddSingleton<ZoneWarTickService>();
        services.AddHostedService(static provider => provider.GetRequiredService<ZoneWarTickService>());

        services.AddSingleton<ZoneCenterRegularWarEventSink>();
        services.AddSingleton<RegularWarRewardGrantSink>();
        services.AddSingleton<IRegularWarEventSink>(sp => new CompositeRegularWarEventSink(
            [
                sp.GetRequiredService<ZoneCenterRegularWarEventSink>(),
                sp.GetRequiredService<RegularWarRewardGrantSink>()
            ],
            sp.GetRequiredService<ILogger<CompositeRegularWarEventSink>>()));
        services.AddSingleton<IRegularWarRewardValueProvider, RegularWarRewardValueProvider>();
        services.AddSingleton<RegularWarSchedulerHost>();
        services.AddHostedService(static provider => provider.GetRequiredService<RegularWarSchedulerHost>());

        services.AddHostedService<TribeVoteElectionCalendarHost>();

        services.AddSingleton(GuardPostCatalogFactory.BuildLive());
        services.AddSingleton(TribeSymbolPlacementCatalog.Default);
        services.AddSingleton<TribeGuardOptions>();
        services.AddSingleton<TribeGuardSpawner>();
        services.AddSingleton<ISimulationSystem>(sp => sp.GetRequiredService<TribeGuardSpawner>());
        services.AddSingleton<TribeSymbolSpawner>();
        services.AddSingleton<ISimulationSystem>(sp => sp.GetRequiredService<TribeSymbolSpawner>());

        services.AddSingleton(TribeGuardCorridorCatalogFactory.BuildLive());

        services.AddSingleton(static provider => PortalProximityCatalog.FromWorldData(
            provider.GetRequiredService<WorldDataCache>()));

        services.AddSingleton<TribeGuardCorridorState>();
        services.AddSingleton<TribeGuardCorridorStateDerivationSystem>();
        services.AddSingleton<ISimulationSystem>(sp =>
            sp.GetRequiredService<TribeGuardCorridorStateDerivationSystem>());

        services.AddSingleton<AllianceCooldownTracker>();
        services.AddSingleton(sp => new AllianceDiplomacyCeremony(
            sp.GetRequiredService<WorldStateService>(),
            sp.GetRequiredService<AllianceCooldownTracker>(),
            sp.GetRequiredService<ZoneEventBroadcaster>(),
            sp.GetRequiredService<ILogger<AllianceDiplomacyCeremony>>(),
            AllianceDiplomacyCeremony.NegotiationConfirmationDurationRawTicks,
            AllianceDiplomacyCeremony.NegotiationConfirmationDurationRawTicks));
        services.AddHostedService<AllianceDiplomacyCeremonyHost>();

        services.AddSingleton<TribeQuotaRegistry>();
        services.AddSingleton<TempRegistrationIdleSweep>();
        services.AddHostedService<TempRegistrationIdleSweepHost>();

        AddHolyStoneScheduling(services);
    }

    private static void AddHolyStoneScheduling(IServiceCollection services)
    {
        services.TryAddSingleton<IHolyStoneCaptureRewardGateway, HolyStoneCaptureRewardGateway>();
        services.TryAddSingleton<IHolyStoneForcedReturnGateway, HolyStoneForcedReturnGateway>();

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
                siegeIngestor: sp.GetRequiredService<ZoneCenterBroadcastIngestor>());
        });
        services.AddHostedService<HolyStoneWarCycleHost>();

        services.AddSingleton(sp => new HolyStoneTerritoryEvictionSweep(
            sp.GetRequiredService<WorldStateService>(),
            sp.GetRequiredService<ZoneRegistry>(),
            [.. ScheduledZoneCenterEventCodes.Zone039ArmingGatedMapIds],
            sp.GetRequiredService<IHolyStoneForcedReturnGateway>(),
            sp.GetRequiredService<ILogger<HolyStoneTerritoryEvictionSweep>>()));
        services.AddHostedService<HolyStoneTerritoryEvictionSweepHost>();
    }

    private static void AddMonsterBossRespawnTracking(IServiceCollection services)
    {
        services.AddSingleton<MonsterBossRespawnTracker>();
        services.AddHostedService<MonsterBossRespawnWriteBehindHost>();
    }
}
