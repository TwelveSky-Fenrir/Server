using CaeriusNet.Abstractions;
using CaeriusNet.Builders;
using Fenrir.Data.Abstractions.Guilds;
using Fenrir.Data.Guilds;
using Fenrir.Data.Tests.Fixtures;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.DependencyInjection;

namespace Fenrir.Data.Tests.Game;

[Collection("SqlServer")]
public class FourGuildScoringProcTests
{
    private readonly string _connectionString;
    private readonly IFourGuildScoringRepository _scoring;

    public FourGuildScoringProcTests(SqlServerFixture fixture)
    {
        var services = CaeriusNetBuilder
            .Create(new ServiceCollection())
            .WithSqlServer(fixture.ConnectionString)
            .Build();

        var db = services.BuildServiceProvider().GetRequiredService<ICaeriusNetDbContext>();
        _scoring = new FourGuildScoringRepository(db);
        _connectionString = fixture.ConnectionString;
    }

    [Fact]
    public async Task AddPoints_IncrementsTheGuildsPointTotal()
    {
        var guildId = await CreateGuildAsync(0);

        await _scoring.AddPointsAsync(guildId, 1, CancellationToken.None);
        await _scoring.AddPointsAsync(guildId, 1, CancellationToken.None);

        Assert.Equal(2, await PointsAsync(guildId));
    }

    [Fact]
    public async Task AddPoints_UnknownGuild_IsSilent_NoThrow()
    {
        var exception = await Record.ExceptionAsync(() =>
            _scoring.AddPointsAsync(-999_999, 1, CancellationToken.None).AsTask());

        Assert.Null(exception);
    }

    [Fact]
    public async Task GetLeaderboard_ReturnsOnlyPositivePointGuilds_HighestFirst()
    {
        await ZeroAllGuildPointsAsync();
        var high = await CreateGuildAsync(10);
        await CreateGuildAsync(0);
        var mid = await CreateGuildAsync(5);

        var board = await _scoring.GetLeaderboardAsync(10, CancellationToken.None);

        Assert.Equal(2, board.Count);
        Assert.Equal(high, board[0].GuildId);
        Assert.Equal(10, board[0].Points);
        Assert.Equal(mid, board[1].GuildId);
        Assert.Equal(5, board[1].Points);
    }

    private async Task<int> CreateGuildAsync(int points)
    {
        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();
        await using var command = new SqlCommand(
            "INSERT INTO game.Guilds (Name, Points) OUTPUT INSERTED.GuildId VALUES (@Name, @Points);",
            connection);
        command.Parameters.AddWithValue("Name", $"fg{Guid.NewGuid():N}"[..12]);
        command.Parameters.AddWithValue("Points", points);
        return (int)(await command.ExecuteScalarAsync())!;
    }

    private async Task<int> PointsAsync(int guildId)
    {
        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();
        await using var command =
            new SqlCommand("SELECT Points FROM game.Guilds WHERE GuildId = @GuildId;", connection);
        command.Parameters.AddWithValue("GuildId", guildId);
        return (int)(await command.ExecuteScalarAsync())!;
    }

    private async Task ZeroAllGuildPointsAsync()
    {
        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();
        await using var command = new SqlCommand("UPDATE game.Guilds SET Points = 0;", connection);
        await command.ExecuteNonQueryAsync();
    }
}
