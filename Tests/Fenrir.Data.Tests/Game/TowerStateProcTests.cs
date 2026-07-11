using CaeriusNet.Abstractions;
using CaeriusNet.Builders;
using Fenrir.Data.Abstractions.Progression;
using Fenrir.Data.Progression;
using Fenrir.Data.Tests.Fixtures;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.DependencyInjection;

namespace Fenrir.Data.Tests.Game;

[Collection("SqlServer")]
public class TowerStateProcTests
{
    private readonly string _connectionString;
    private readonly ITowerRepository _towers;

    public TowerStateProcTests(SqlServerFixture fixture)
    {
        var services = CaeriusNetBuilder
            .Create(new ServiceCollection())
            .WithSqlServer(fixture.ConnectionString)
            .Build();

        var db = services.BuildServiceProvider().GetRequiredService<ICaeriusNetDbContext>();
        _towers = new TowerRepository(db);
        _connectionString = fixture.ConnectionString;
    }

    [Fact]
    public async Task EnsureInitializedAsync_IsIdempotent_AndSeedsAllTwelveUncontrolledTowers()
    {
        await _towers.EnsureInitializedAsync(CancellationToken.None);
        await _towers.EnsureInitializedAsync(CancellationToken.None);

        var rows = await _towers.GetAllAsync(CancellationToken.None);

        Assert.Equal(12, rows.Count);
        for (byte i = 0; i < 12; i++)
        {
            var row = Assert.Single(rows, r => r.TowerIndex == i);
            Assert.True(
                row.Level == 0 || row.ControllingTribeId is not null,
                $"TowerIndex {i} should exist regardless of its current progress.");
        }
    }

    [Fact]
    public async Task SetProgressAsync_WithATribe_PersistsLevelTypeAndCaptureMoment_ReadBackByGetAll()
    {
        await _towers.EnsureInitializedAsync(CancellationToken.None);
        const byte towerIndex = 5;
        var tribeId = await EnsureTribeExistsAsync(2);

        await _towers.SetProgressAsync(towerIndex, 4, 2, tribeId, CancellationToken.None);

        var rows = await _towers.GetAllAsync(CancellationToken.None);
        var row = Assert.Single(rows, r => r.TowerIndex == towerIndex);
        Assert.Equal((byte)4, row.Level);
        Assert.Equal((byte)2, row.TowerType);
        Assert.Equal(tribeId, row.ControllingTribeId);
        Assert.NotNull(row.CapturedAtUtc);
    }

    [Fact]
    public async Task SetProgressAsync_WithNullTribe_ClearsTheCaptureMoment_ButKeepsTheLevelAndType()
    {
        await _towers.EnsureInitializedAsync(CancellationToken.None);
        const byte towerIndex = 6;
        var tribeId = await EnsureTribeExistsAsync(3);
        await _towers.SetProgressAsync(towerIndex, 6, 3, tribeId, CancellationToken.None);

        await _towers.SetProgressAsync(towerIndex, 0, 0, null, CancellationToken.None);

        var rows = await _towers.GetAllAsync(CancellationToken.None);
        var row = Assert.Single(rows, r => r.TowerIndex == towerIndex);
        Assert.Equal((byte)0, row.Level);
        Assert.Equal((byte)0, row.TowerType);
        Assert.Null(row.ControllingTribeId);
        Assert.Null(row.CapturedAtUtc);
    }

    private async Task<byte> EnsureTribeExistsAsync(byte tribeId)
    {
        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();
        await using var command = new SqlCommand(
            "IF NOT EXISTS (SELECT 1 FROM game.Tribes WHERE TribeId = @TribeId) " +
            "INSERT INTO game.Tribes (TribeId) VALUES (@TribeId);", connection);
        command.Parameters.AddWithValue("TribeId", tribeId);
        await command.ExecuteNonQueryAsync();
        return tribeId;
    }
}
