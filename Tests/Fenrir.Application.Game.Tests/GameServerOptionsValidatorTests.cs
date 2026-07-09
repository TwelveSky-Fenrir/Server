using Fenrir.Application.Game.Domain;

namespace Fenrir.Application.Game.Tests;

public class GameServerOptionsValidatorTests
{
    private static readonly GameServerOptionsValidator Validator = new();

    // GameServerOptions is a plain sealed class, not a record -- no "with", so build a fresh instance per call
    private static GameServerOptions Options(
        int port = 1100,
        byte shardId = 1,
        string? publicHost = "127.0.0.1",
        int tickRateHz = 20,
        float aoiCellSize = 75f,
        float maxPlausibleMoveDistance = 666f,
        int heartbeatIntervalSeconds = 5,
        int capacity = 300,
        string? gameDataDirectory = "GameData",
        int heroRankingRolloverCheckIntervalMinutes = 60,
        int accountSessionPollIntervalSeconds = 20,
        int tempRegistrationIdleSweepIntervalSeconds = 30,
        bool voteTribeEnabled = false,
        short voteTribeMapId = 0,
        bool holyStoneBattleEnabled = false,
        short tribeSymbolBattleMapId = 0,
        bool holyStoneWarEnabled = false,
        short holyStoneMapId = 0,
        bool allianceTribeEnabled = false,
        short allianceTribeMapId = 0,
        bool monsterSymbolAttackNotifyEnabled = false,
        int monsterSymbolAttackNotifyDelayMinutes = 0,
        bool holyStoneTestMode = false,
        ISet<DayOfWeek>? holyStoneBattleDays = null,
        IReadOnlyDictionary<byte, short>? monsterSymbolAttackNotifyMapIds = null)
    {
        return new GameServerOptions
        {
            Port = port,
            ShardId = shardId,
            PublicHost = publicHost!,
            TickRateHz = tickRateHz,
            AoiCellSize = aoiCellSize,
            MaxPlausibleMoveDistance = maxPlausibleMoveDistance,
            HeartbeatIntervalSeconds = heartbeatIntervalSeconds,
            Capacity = capacity,
            GameDataDirectory = gameDataDirectory!,
            HeroRankingRolloverCheckIntervalMinutes = heroRankingRolloverCheckIntervalMinutes,
            AccountSessionPollIntervalSeconds = accountSessionPollIntervalSeconds,
            TempRegistrationIdleSweepIntervalSeconds = tempRegistrationIdleSweepIntervalSeconds,
            VoteTribeEnabled = voteTribeEnabled,
            VoteTribeMapId = voteTribeMapId,
            HolyStoneBattleEnabled = holyStoneBattleEnabled,
            TribeSymbolBattleMapId = tribeSymbolBattleMapId,
            HolyStoneWarEnabled = holyStoneWarEnabled,
            HolyStoneMapId = holyStoneMapId,
            AllianceTribeEnabled = allianceTribeEnabled,
            AllianceTribeMapId = allianceTribeMapId,
            MonsterSymbolAttackNotifyEnabled = monsterSymbolAttackNotifyEnabled,
            MonsterSymbolAttackNotifyDelayMinutes = monsterSymbolAttackNotifyDelayMinutes,
            HolyStoneTestMode = holyStoneTestMode,
            HolyStoneBattleDays = holyStoneBattleDays ?? new HashSet<DayOfWeek>(),
            MonsterSymbolAttackNotifyMapIds = monsterSymbolAttackNotifyMapIds ?? new Dictionary<byte, short>()
        };
    }

    [Fact]
    public void Validate_DefaultOptions_Succeeds()
    {
        var result = Validator.Validate(null, Options());

        Assert.True(result.Succeeded);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(65536)]
    [InlineData(-1)]
    public void Validate_PortOutOfRange_Fails(int port)
    {
        var result = Validator.Validate(null, Options(port));

        Assert.True(result.Failed);
        Assert.Contains(result.Failures!, f => f.Contains("Game:Port"));
    }

    [Fact]
    public void Validate_ShardIdZero_Fails()
    {
        var result = Validator.Validate(null, Options(shardId: 0));

        Assert.True(result.Failed);
        Assert.Contains(result.Failures!, f => f.Contains("Game:ShardId"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Validate_PublicHostEmpty_Fails(string? publicHost)
    {
        var result = Validator.Validate(null, Options(publicHost: publicHost));

        Assert.True(result.Failed);
        Assert.Contains(result.Failures!, f => f.Contains("Game:PublicHost"));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Validate_TickRateHzNotPositive_Fails(int tickRateHz)
    {
        var result = Validator.Validate(null, Options(tickRateHz: tickRateHz));

        Assert.True(result.Failed);
        Assert.Contains(result.Failures!, f => f.Contains("Game:TickRateHz"));
    }

    [Theory]
    [InlineData(0f)]
    [InlineData(-1f)]
    public void Validate_AoiCellSizeNotPositive_Fails(float aoiCellSize)
    {
        var result = Validator.Validate(null, Options(aoiCellSize: aoiCellSize));

        Assert.True(result.Failed);
        Assert.Contains(result.Failures!, f => f.Contains("Game:AoiCellSize"));
    }

    [Theory]
    [InlineData(0f)]
    [InlineData(-1f)]
    public void Validate_MaxPlausibleMoveDistanceNotPositive_Fails(float distance)
    {
        var result = Validator.Validate(null, Options(maxPlausibleMoveDistance: distance));

        Assert.True(result.Failed);
        Assert.Contains(result.Failures!, f => f.Contains("Game:MaxPlausibleMoveDistance"));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Validate_HeartbeatIntervalSecondsNotPositive_Fails(int heartbeatIntervalSeconds)
    {
        var result = Validator.Validate(null, Options(heartbeatIntervalSeconds: heartbeatIntervalSeconds));

        Assert.True(result.Failed);
        Assert.Contains(result.Failures!, f => f.Contains("Game:HeartbeatIntervalSeconds"));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Validate_CapacityNotPositive_Fails(int capacity)
    {
        var result = Validator.Validate(null, Options(capacity: capacity));

        Assert.True(result.Failed);
        Assert.Contains(result.Failures!, f => f.Contains("Game:Capacity"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Validate_GameDataDirectoryEmpty_Fails(string? gameDataDirectory)
    {
        var result = Validator.Validate(null, Options(gameDataDirectory: gameDataDirectory));

        Assert.True(result.Failed);
        Assert.Contains(result.Failures!, f => f.Contains("Game:GameDataDirectory"));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Validate_HeroRankingRolloverCheckIntervalMinutesNotPositive_Fails(int intervalMinutes)
    {
        var result = Validator.Validate(null, Options(heroRankingRolloverCheckIntervalMinutes: intervalMinutes));

        Assert.True(result.Failed);
        Assert.Contains(result.Failures!, f => f.Contains("Game:HeroRankingRolloverCheckIntervalMinutes"));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Validate_AccountSessionPollIntervalSecondsNotPositive_Fails(int intervalSeconds)
    {
        var result = Validator.Validate(null, Options(accountSessionPollIntervalSeconds: intervalSeconds));

        Assert.True(result.Failed);
        Assert.Contains(result.Failures!, f => f.Contains("Game:AccountSessionPollIntervalSeconds"));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Validate_TempRegistrationIdleSweepIntervalSecondsNotPositive_Fails(int intervalSeconds)
    {
        var result = Validator.Validate(null, Options(tempRegistrationIdleSweepIntervalSeconds: intervalSeconds));

        Assert.True(result.Failed);
        Assert.Contains(result.Failures!, f => f.Contains("Game:TempRegistrationIdleSweepIntervalSeconds"));
    }

    [Fact]
    public void Validate_VoteTribeEnabledWithoutMapId_Fails()
    {
        var result = Validator.Validate(null, Options(voteTribeEnabled: true));

        Assert.True(result.Failed);
        Assert.Contains(result.Failures!, f => f.Contains("Game:VoteTribeMapId"));
    }

    [Fact]
    public void Validate_VoteTribeEnabledWithMapId_Succeeds()
    {
        var result = Validator.Validate(null, Options(voteTribeEnabled: true, voteTribeMapId: 37));

        Assert.True(result.Succeeded);
    }

    [Fact]
    public void Validate_VoteTribeDisabled_SucceedsRegardlessOfMapId()
    {
        var result = Validator.Validate(null, Options(voteTribeMapId: 0));

        Assert.True(result.Succeeded);
    }

    [Fact]
    public void Validate_HolyStoneBattleEnabledWithoutMapId_Fails()
    {
        var result = Validator.Validate(null, Options(holyStoneBattleEnabled: true));

        Assert.True(result.Failed);
        Assert.Contains(result.Failures!, f => f.Contains("Game:TribeSymbolBattleMapId"));
    }

    [Fact]
    public void Validate_HolyStoneBattleEnabledWithMapId_Succeeds()
    {
        var result = Validator.Validate(null,
            Options(holyStoneBattleEnabled: true, tribeSymbolBattleMapId: 37,
                holyStoneBattleDays: new HashSet<DayOfWeek> { DayOfWeek.Saturday }));

        Assert.True(result.Succeeded);
    }

    [Fact]
    public void Validate_HolyStoneBattleEnabledWithoutBattleDaysOutsideTestMode_Fails()
    {
        var result = Validator.Validate(null, Options(holyStoneBattleEnabled: true, tribeSymbolBattleMapId: 37));

        Assert.True(result.Failed);
        Assert.Contains(result.Failures!, f => f.Contains("Game:HolyStoneBattleDays"));
    }

    [Fact]
    public void Validate_HolyStoneBattleEnabledWithoutBattleDaysInTestMode_Succeeds()
    {
        var result = Validator.Validate(null,
            Options(holyStoneBattleEnabled: true, tribeSymbolBattleMapId: 37, holyStoneTestMode: true));

        Assert.True(result.Succeeded);
    }

    [Fact]
    public void Validate_HolyStoneWarEnabledWithoutMapId_Fails()
    {
        var result = Validator.Validate(null, Options(holyStoneWarEnabled: true));

        Assert.True(result.Failed);
        Assert.Contains(result.Failures!, f => f.Contains("Game:HolyStoneMapId"));
    }

    [Fact]
    public void Validate_HolyStoneWarEnabledWithMapId_Succeeds()
    {
        var result = Validator.Validate(null, Options(holyStoneWarEnabled: true, holyStoneMapId: 38));

        Assert.True(result.Succeeded);
    }

    [Fact]
    public void Validate_AllianceTribeEnabledWithoutMapId_Fails()
    {
        var result = Validator.Validate(null, Options(allianceTribeEnabled: true));

        Assert.True(result.Failed);
        Assert.Contains(result.Failures!, f => f.Contains("Game:AllianceTribeMapId"));
    }

    [Fact]
    public void Validate_AllianceTribeEnabledWithMapId_Succeeds()
    {
        var result = Validator.Validate(null, Options(allianceTribeEnabled: true, allianceTribeMapId: 37));

        Assert.True(result.Succeeded);
    }

    [Fact]
    public void Validate_MonsterSymbolAttackNotifyEnabledWithoutDelayMinutes_Fails()
    {
        var result = Validator.Validate(null, Options(monsterSymbolAttackNotifyEnabled: true));

        Assert.True(result.Failed);
        Assert.Contains(result.Failures!, f => f.Contains("Game:MonsterSymbolAttackNotifyDelayMinutes"));
    }

    [Fact]
    public void Validate_MonsterSymbolAttackNotifyEnabledWithNegativeDelayMinutes_Fails()
    {
        var result = Validator.Validate(null,
            Options(monsterSymbolAttackNotifyEnabled: true, monsterSymbolAttackNotifyDelayMinutes: -1));

        Assert.True(result.Failed);
        Assert.Contains(result.Failures!, f => f.Contains("Game:MonsterSymbolAttackNotifyDelayMinutes"));
    }

    [Fact]
    public void Validate_MonsterSymbolAttackNotifyEnabledWithPositiveDelayMinutes_Succeeds()
    {
        var result = Validator.Validate(null,
            Options(monsterSymbolAttackNotifyEnabled: true, monsterSymbolAttackNotifyDelayMinutes: 30,
                monsterSymbolAttackNotifyMapIds: new Dictionary<byte, short>
                {
                    [0] = 4,
                    [1] = 9,
                    [2] = 14,
                    [3] = 143
                }));

        Assert.True(result.Succeeded);
    }

    [Fact]
    public void Validate_MonsterSymbolAttackNotifyDisabled_SucceedsRegardlessOfDelayMinutes()
    {
        var result = Validator.Validate(null, Options(monsterSymbolAttackNotifyDelayMinutes: 0));

        Assert.True(result.Succeeded);
    }

    [Fact]
    public void Validate_MonsterSymbolAttackNotifyEnabledWithEmptyMapIds_Fails()
    {
        var result = Validator.Validate(null,
            Options(monsterSymbolAttackNotifyEnabled: true, monsterSymbolAttackNotifyDelayMinutes: 30));

        Assert.True(result.Failed);
        Assert.Contains(result.Failures!, f => f.Contains("Game:MonsterSymbolAttackNotifyMapIds"));
    }

    [Fact]
    public void Validate_MonsterSymbolAttackNotifyEnabledWithPartialMapIds_Fails()
    {
        var result = Validator.Validate(null,
            Options(monsterSymbolAttackNotifyEnabled: true, monsterSymbolAttackNotifyDelayMinutes: 30,
                monsterSymbolAttackNotifyMapIds: new Dictionary<byte, short> { [0] = 4, [1] = 9 }));

        Assert.True(result.Failed);
        Assert.Contains(result.Failures!, f => f.Contains("Game:MonsterSymbolAttackNotifyMapIds"));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Validate_MaxConnectionsPerIpNotPositive_Fails(int maxConnectionsPerIp)
    {
        var options = Options();
        options.MaxConnectionsPerIp = maxConnectionsPerIp;
        var result = Validator.Validate(null, options);

        Assert.True(result.Failed);
        Assert.Contains(result.Failures!, f => f.Contains("Game:MaxConnectionsPerIp"));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Validate_MaxProtocolViolationsPerIpPerHourNotPositive_Fails(int maxProtocolViolationsPerIpPerHour)
    {
        var options = Options();
        options.MaxProtocolViolationsPerIpPerHour = maxProtocolViolationsPerIpPerHour;
        var result = Validator.Validate(null, options);

        Assert.True(result.Failed);
        Assert.Contains(result.Failures!, f => f.Contains("Game:MaxProtocolViolationsPerIpPerHour"));
    }

    [Theory]
    [InlineData(0f)]
    [InlineData(-1f)]
    public void Validate_AlliancePostRadiusNotPositive_Fails(float alliancePostRadius)
    {
        var options = Options();
        options.AlliancePostRadius = alliancePostRadius;
        var result = Validator.Validate(null, options);

        Assert.True(result.Failed);
        Assert.Contains(result.Failures!, f => f.Contains("Game:AlliancePostRadius"));
    }

    [Theory]
    [InlineData(0f)]
    [InlineData(-1f)]
    public void Validate_HolyStoneCaptureRadiusNotPositive_Fails(float holyStoneCaptureRadius)
    {
        var options = Options();
        options.HolyStoneCaptureRadius = holyStoneCaptureRadius;
        var result = Validator.Validate(null, options);

        Assert.True(result.Failed);
        Assert.Contains(result.Failures!, f => f.Contains("Game:HolyStoneCaptureRadius"));
    }

    [Theory]
    [InlineData(0f)]
    [InlineData(-1f)]
    public void Validate_HolyStoneParticipationRadiusNotPositive_Fails(float holyStoneParticipationRadius)
    {
        var options = Options();
        options.HolyStoneParticipationRadius = holyStoneParticipationRadius;
        var result = Validator.Validate(null, options);

        Assert.True(result.Failed);
        Assert.Contains(result.Failures!, f => f.Contains("Game:HolyStoneParticipationRadius must be positive"));
    }

    [Fact]
    public void Validate_HolyStoneParticipationRadiusLessThanCaptureRadius_Fails()
    {
        var options = Options();
        options.HolyStoneCaptureRadius = 5f;
        options.HolyStoneParticipationRadius = 2f;
        var result = Validator.Validate(null, options);

        Assert.True(result.Failed);
        Assert.Contains(result.Failures!, f => f.Contains("Game:HolyStoneParticipationRadius"));
    }

    [Fact]
    public void Validate_GuildTribeBroadcastRetentionNotGreaterThanPollInterval_Fails()
    {
        var options = Options();
        options.GuildTribeBroadcastPollIntervalSeconds = 30;
        options.GuildTribeBroadcastRetentionSeconds = 30;
        var result = Validator.Validate(null, options);

        Assert.True(result.Failed);
        Assert.Contains(result.Failures!, f => f.Contains("Game:GuildTribeBroadcastRetentionSeconds"));
    }

    [Fact]
    public void Validate_SocialCrossShardRelayRetentionNotGreaterThanPollInterval_Fails()
    {
        var options = Options();
        options.SocialCrossShardRelayPollIntervalSeconds = 30;
        options.SocialCrossShardRelayRetentionSeconds = 10;
        var result = Validator.Validate(null, options);

        Assert.True(result.Failed);
        Assert.Contains(result.Failures!, f => f.Contains("Game:SocialCrossShardRelayRetentionSeconds"));
    }

    [Fact]
    public void Validate_ProxyShopExpirationRelayRetentionNotGreaterThanPollInterval_Fails()
    {
        var options = Options();
        options.ProxyShopExpirationRelayPollIntervalSeconds = 30;
        options.ProxyShopExpirationRelayRetentionSeconds = 30;
        var result = Validator.Validate(null, options);

        Assert.True(result.Failed);
        Assert.Contains(result.Failures!, f => f.Contains("Game:ProxyShopExpirationRelayRetentionSeconds"));
    }

    [Fact]
    public void Validate_RvrSiegeEventRelayRetentionNotGreaterThanPollInterval_Fails()
    {
        var options = Options();
        options.RvrSiegeEventRelayPollIntervalSeconds = 30;
        options.RvrSiegeEventRelayRetentionSeconds = 30;
        var result = Validator.Validate(null, options);

        Assert.True(result.Failed);
        Assert.Contains(result.Failures!, f => f.Contains("Game:RvrSiegeEventRelayRetentionSeconds"));
    }

    [Fact]
    public void Validate_GuildBuffExpiryRelayRetentionNotGreaterThanPollInterval_Fails()
    {
        var options = Options();
        options.GuildBuffExpiryRelayPollIntervalSeconds = 30;
        options.GuildBuffExpiryRelayRetentionSeconds = 30;
        var result = Validator.Validate(null, options);

        Assert.True(result.Failed);
        Assert.Contains(result.Failures!, f => f.Contains("Game:GuildBuffExpiryRelayRetentionSeconds"));
    }
}
