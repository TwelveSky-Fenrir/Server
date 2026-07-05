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
        float maxPlausibleSpeedPerSecond = 20f,
        int heartbeatIntervalSeconds = 5,
        int capacity = 300,
        string? gameDataDirectory = "GameData",
        int heroRankingRolloverCheckIntervalMinutes = 60)
    {
        return new GameServerOptions
        {
            Port = port,
            ShardId = shardId,
            PublicHost = publicHost!,
            TickRateHz = tickRateHz,
            AoiCellSize = aoiCellSize,
            MaxPlausibleSpeedPerSecond = maxPlausibleSpeedPerSecond,
            HeartbeatIntervalSeconds = heartbeatIntervalSeconds,
            Capacity = capacity,
            GameDataDirectory = gameDataDirectory!,
            HeroRankingRolloverCheckIntervalMinutes = heroRankingRolloverCheckIntervalMinutes
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
    public void Validate_MaxPlausibleSpeedPerSecondNotPositive_Fails(float speed)
    {
        var result = Validator.Validate(null, Options(maxPlausibleSpeedPerSecond: speed));

        Assert.True(result.Failed);
        Assert.Contains(result.Failures!, f => f.Contains("Game:MaxPlausibleSpeedPerSecond"));
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
}
