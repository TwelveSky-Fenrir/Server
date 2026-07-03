namespace Fenrir.Application.Game.Tests;

public class GameServerOptionsValidatorTests
{
    private static readonly GameServerOptionsValidator Validator = new();

    // GameServerOptions is a plain sealed class (not a record), so "with" expressions are not available --
    // build a fresh, fully-valid instance per call and override only the field under test. Maps defaults to
    // [1] HERE because the compiled-in GameServerOptions default is deliberately empty (binder concatenation,
    // see the property's remarks) -- the valid baseline must supply it like appsettings.json does.
    private static GameServerOptions Options(
        int port = 1100,
        byte shardId = 1,
        short[]? maps = null,
        string? publicHost = "127.0.0.1",
        int tickRateHz = 20,
        float aoiCellSize = 75f,
        float maxPlausibleSpeedPerSecond = 20f,
        int heartbeatIntervalSeconds = 5,
        int capacity = 300,
        string? gameDataDirectory = "GameData")
    {
        return new GameServerOptions
        {
            Port = port,
            ShardId = shardId,
            Maps = maps ?? [1],
            PublicHost = publicHost!,
            TickRateHz = tickRateHz,
            AoiCellSize = aoiCellSize,
            MaxPlausibleSpeedPerSecond = maxPlausibleSpeedPerSecond,
            HeartbeatIntervalSeconds = heartbeatIntervalSeconds,
            Capacity = capacity,
            GameDataDirectory = gameDataDirectory!
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

    [Fact]
    public void Validate_MultipleDistinctMaps_Succeeds()
    {
        var result = Validator.Validate(null, Options(maps: [1, 2, 33]));

        Assert.True(result.Succeeded);
    }

    // The empty case is exactly what an unbound GameServerOptions looks like (compiled-in default is [],
    // see the property's remarks) -- it must fail loudly at ValidateOnStart, not boot a shard with no world.
    [Fact]
    public void Validate_MapsEmpty_Fails()
    {
        var result = Validator.Validate(null, Options(maps: []));

        Assert.True(result.Failed);
        Assert.Contains(result.Failures!, f => f.Contains("Game:Maps"));
    }

    [Theory]
    [InlineData((short)0)]
    [InlineData((short)-1)]
    public void Validate_MapsEntryNotPositive_Fails(short mapId)
    {
        var result = Validator.Validate(null, Options(maps: [1, mapId]));

        Assert.True(result.Failed);
        Assert.Contains(result.Failures!, f => f.Contains("Game:Maps"));
    }

    // Duplicates are how the binder-concatenation trap (non-empty compiled-in default + bound entries)
    // would manifest -- the validator is the tripwire that turns it into a clear startup failure.
    [Fact]
    public void Validate_MapsDuplicate_Fails()
    {
        var result = Validator.Validate(null, Options(maps: [1, 2, 1]));

        Assert.True(result.Failed);
        Assert.Contains(result.Failures!, f => f.Contains("Game:Maps"));
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
}
