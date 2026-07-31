using Fenrir.Application.Game.Domain.Mounts;
using Fenrir.Application.Game.Domain.World.Configuration;
using Fenrir.Application.Game.Domain.World.WorldState;
using Fenrir.Application.Game.Domain.World.ZoneWar;

namespace Fenrir.Application.Game.Domain;

public sealed class GameServerOptions
{
    public int Port { get; set; } = 1100;

    public int ZoneBasePort { get; set; } = 1100;

    // Bloc de ports que l'orchestrateur a REELLEMENT reserve pour les listeners de zone. Le shard verifie
    // au boot que chaque port derive (ZoneBasePort + mapId) y tombe : il ne peut pas deplacer un port, le
    // plan d'adressage etant fige par le client legacy, mais il peut refuser de binder hors du bloc.
    public int ZonePortRangeStart { get; set; } = 1101;

    public int ZonePortRangeEnd { get; set; } = 1449;

    // Ports que l'orchestrateur a attribues a d'autres ressources ; un port de zone qui tomberait dessus
    // est un conflit, pas une coincidence.
    public string ReservedPorts { get; set; } = "";

    public byte ShardId { get; set; } = 1;

    public string GameDataDirectory { get; set; } = "GameData";

    public string PublicHost { get; set; } = "127.0.0.1";

    public int TicketTtlSeconds { get; set; } = 15;

    public int TickRateHz { get; set; } = 20;

    public float AoiCellSize { get; set; } = 1000f;

    public float MaxPlausibleMoveDistance { get; set; } = 666f;

    public int HeartbeatIntervalSeconds { get; set; } = 5;

    public int HeroRankingRolloverCheckIntervalMinutes { get; set; } = 60;

    public int Capacity { get; set; } = 300;

    public int AccountSessionPollIntervalSeconds { get; set; } = 2;

    public int MutePollIntervalSeconds { get; set; } = 15;

    public int MaxConnectionsPerIp { get; set; } = 40;

    public int MaxProtocolViolationsPerIpPerHour { get; set; } = 30;

    public bool VoteTribeEnabled { get; set; }

    public short VoteTribeMapId { get; set; }

    public bool VoteTribeTestMode { get; set; }

    public bool AllianceTribeEnabled { get; set; }

    public short AllianceTribeMapId { get; set; }

    public float AlliancePost0X { get; set; } = -41f;

    public float AlliancePost0Z { get; set; } = -272f;

    public float AlliancePost1X { get; set; } = 35f;

    public float AlliancePost1Z { get; set; } = -272f;

    public float AlliancePostRadius { get; set; } = 10f;

    public ISet<short> Zone241DungeonMapIds { get; set; } = new HashSet<short>();

    public ISet<short> Zone126TypeMapIds { get; set; } = new HashSet<short>();

    public ISet<short> Zone039TypeMapIds { get; set; } = new HashSet<short>();

    public bool HolyStoneBattleEnabled { get; set; }

    public short TribeSymbolBattleMapId { get; set; }

    public bool HolyStoneWarEnabled { get; set; }

    public bool HolyStoneTestMode { get; set; }

    public ISet<DayOfWeek> HolyStoneBattleDays { get; set; } = new HashSet<DayOfWeek>();

    public short HolyStoneMapId { get; set; }

    public float HolyStoneX { get; set; }
    public float HolyStoneZ { get; set; }
    public float HolyStoneCaptureRadius { get; set; } = 1f;
    public float HolyStoneParticipationRadius { get; set; } = 1f;

    public ISet<short> HolyStoneTerritoryMapIds { get; set; } = new HashSet<short>();

    public TribeQuotaGroup TribeQuotaGroup { get; set; } = TribeQuotaGroup.None;

    public int TempRegistrationIdleSweepIntervalSeconds { get; set; } = 30;

    public int SessionLivenessSweepIntervalSeconds { get; set; } = 30;

    public bool TribeFourConversionEnabled { get; set; }

    public short ForcedNeutralResetHomeMapId { get; set; }

    public short FactionTransferHomeZoneMapId { get; set; }

    public int GuildTribeBroadcastPollIntervalSeconds { get; set; } = 2;

    public int GuildTribeBroadcastRetentionSeconds { get; set; } = 30;

    public int SocialCrossShardRelayPollIntervalSeconds { get; set; } = 1;

    public int SocialCrossShardRelayRetentionSeconds { get; set; } = 30;

    public int ChatCrossShardRelayPollIntervalSeconds { get; set; } = 1;

    public int ChatCrossShardRelayRetentionSeconds { get; set; } = 30;

    public int ProxyShopExpirationRelayPollIntervalSeconds { get; set; } = 2;

    public int ProxyShopExpirationRelayRetentionSeconds { get; set; } = 30;

    public int RvrSiegeEventRelayPollIntervalSeconds { get; set; } = 2;

    public int RvrSiegeEventRelayRetentionSeconds { get; set; } = 30;

    public ISet<short> Zone195MapIds { get; set; } = new HashSet<short>();

    public bool MonsterSymbolAttackNotifyEnabled { get; set; }

    public int MonsterSymbolAttackNotifyDelayMinutes { get; set; }

    public IReadOnlyDictionary<byte, short> MonsterSymbolAttackNotifyMapIds { get; set; } =
        new Dictionary<byte, short>();

    public int GuildBuffExpiryRelayPollIntervalSeconds { get; set; } = 2;

    public int GuildBuffExpiryRelayRetentionSeconds { get; set; } = 30;

    public int PartyResyncRelayPollIntervalSeconds { get; set; } = 1;

    public int PartyResyncRelayRetentionSeconds { get; set; } = 30;

    public Dictionary<int, ZoneConfig> Zones { get; set; } = new();

    public int MountKillExperiencePerKill { get; set; } =
        MountKillExperienceCalculator.PlaceholderBaseExperiencePerKill;

    public int CrossTribeCpAddValue { get; set; } = 3;

    public int CrossTribeXpRatio { get; set; } = 2;

    public WorldStateAuthorityMode WorldStateAuthority { get; set; } = WorldStateAuthorityMode.Shard;
}
