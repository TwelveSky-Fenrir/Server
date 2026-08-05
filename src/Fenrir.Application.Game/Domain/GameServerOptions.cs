using Fenrir.Application.Game.Domain.Mounts;
using Fenrir.Application.Game.Domain.World.Configuration;

namespace Fenrir.Application.Game.Domain;

public enum GameRuleset
{
    Standard,

    ExtendedUpgrade
}

public static class GameRulesetRules
{
    public const int StandardMaxEnchant = 40;

    public const int ExtendedUpgradeMaxEnchant = 50;

    public static bool TryParse(string? value, out GameRuleset ruleset)
    {
        if (Enum.TryParse(value, true, out GameRuleset parsed) &&
            parsed is GameRuleset.Standard or GameRuleset.ExtendedUpgrade)
        {
            ruleset = parsed;
            return true;
        }

        ruleset = default;
        return false;
    }

    public static int MaxEnchant(GameRuleset ruleset)
    {
        return ruleset switch
        {
            GameRuleset.Standard => StandardMaxEnchant,
            GameRuleset.ExtendedUpgrade => ExtendedUpgradeMaxEnchant,
            _ => throw new ArgumentOutOfRangeException(nameof(ruleset), ruleset, "Unsupported game ruleset.")
        };
    }
}

public sealed class GameServerOptions
{
    public string Ruleset { get; set; } = "";

    public int Port { get; set; } = 1100;

    public int ZoneBasePort { get; set; } = 1100;

    public int ZonePortRangeStart { get; set; } = 1101;

    public int ZonePortRangeEnd { get; set; } = 1449;

    public string ReservedPorts { get; set; } = "";

    public byte ShardId { get; set; } = 1;

    public string GameDataDirectory { get; set; } = "GameData";

    public string PublicHost { get; set; } = "127.0.0.1";

    public int TicketTtlSeconds { get; set; } = 60;

    public int ShardReachabilityProbeTimeoutMilliseconds { get; set; } = 750;

    public int TickRateHz { get; set; } = 20;

    public float AoiCellSize { get; set; } = 1000f;

    public float MaxPlausibleMoveDistance { get; set; } = 666f;

    public int HeartbeatIntervalSeconds { get; set; } = 5;

    public int HeroRankingRolloverCheckIntervalMinutes { get; set; } = 60;

    public int Capacity { get; set; } = 300;

    public int AccountSessionPollIntervalSeconds { get; set; } = 2;

    public int MutePollIntervalSeconds { get; set; } = 15;

    public int MaxConnectionsPerIp { get; set; } = 512;

    public int MaxProtocolViolationsPerIpPerHour { get; set; } = 1_000;

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

    public float MaxAttackPacketPositionDelta { get; set; } = 185f;

    public bool ChallengeContentEnabled { get; set; }

    public bool SecondarySiegeContentEnabled { get; set; }

    public ISet<short> Zone241DungeonMapIds { get; set; } = new HashSet<short>
    {
        241, 242, 243, 244, 245, 246, 247, 248, 249, 292,
        293, 294, 311, 312, 325, 326, 327, 328, 329, 330
    };

    public ISet<short> Zone126TypeMapIds { get; set; } = new HashSet<short>
    {
        126, 130, 134, 171, 127, 131, 135, 172, 128, 132, 136, 173, 129, 133, 137, 174,
        210, 211, 212, 213, 214, 215, 216, 217, 218, 219, 220, 221,
        222, 223, 224, 225, 226, 227, 228, 229, 230, 231, 232, 233
    };

    public ISet<short> Zone039TypeMapIds { get; set; } = new HashSet<short> { 39, 74, 144, 145, 313 };

    public bool HolyStoneBattleEnabled { get; set; }

    public short TribeSymbolBattleMapId { get; set; }

    public bool HolyStoneWarEnabled { get; set; }

    public bool HolyStoneTestMode { get; set; }

    public ISet<DayOfWeek> HolyStoneBattleDays { get; set; } = new HashSet<DayOfWeek>();

    public short HolyStoneMapId { get; set; } = 38;

    public float HolyStoneX { get; set; } = -40f;
    public float HolyStoneY { get; set; } = 200f;
    public float HolyStoneZ { get; set; } = 6379f;
    public float HolyStoneCaptureRadius { get; set; } = 25f;
    public float HolyStoneParticipationRadius { get; set; } = 3500f;

    public ISet<short> HolyStoneTerritoryMapIds { get; set; } = new HashSet<short>();

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

    public int GuildStateRelayPollIntervalSeconds { get; set; } = 2;

    public int GuildStateRelayRetentionSeconds { get; set; } = 30;

    public int PartyResyncRelayPollIntervalSeconds { get; set; } = 1;

    public int PartyResyncRelayRetentionSeconds { get; set; } = 30;

    public Dictionary<int, ZoneConfig> Zones { get; set; } = new();

    public int MountKillExperiencePerKill { get; set; } =
        MountKillExperienceCalculator.DefaultBaseExperiencePerKill;

    public int CrossTribeCpAddValue { get; set; } = 3;

    public int CrossTribeXpRatio { get; set; } = 2;

    public int GlobalExpDownRatio { get; set; } = 1;

    public int TeacherPointUpRatio { get; set; } = 1;

    public float GeneralExpUpRatio { get; set; } = 20f;
}
