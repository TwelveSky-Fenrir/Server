using CaeriusNet.Abstractions;
using CaeriusNet.Builders;
using Fenrir.Data.Admin;
using Fenrir.Data.Tests.Fixtures;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.DependencyInjection;

namespace Fenrir.Data.Tests.Admin;

/// <summary>admin.usp_ShardMapAssignment_GetForShard against real SQL Server 2025.</summary>
[Collection("SqlServer")]
public class ShardMapAssignmentProcTests
{
    private readonly IShardMapAssignmentRepository _repository;
    private readonly string _connectionString;

    public ShardMapAssignmentProcTests(SqlServerFixture fixture)
    {
        var services = CaeriusNetBuilder
            .Create(new ServiceCollection())
            .WithSqlServer(fixture.ConnectionString)
            .Build();

        var db = services.BuildServiceProvider().GetRequiredService<ICaeriusNetDbContext>();
        _repository = new ShardMapAssignmentRepository(db);
        _connectionString = fixture.ConnectionString;
    }

    [Fact]
    public async Task GetHostedMaps_SeededM1Shard_ReturnsMapOne()
    {
        var maps = await _repository.GetHostedMapsAsync(1, CancellationToken.None);

        Assert.Contains((short)1, maps);
    }

    [Fact]
    public async Task GetHostedMaps_UnknownShard_ReturnsEmpty()
    {
        var maps = await _repository.GetHostedMapsAsync(250, CancellationToken.None);

        Assert.Empty(maps);
    }

    [Fact]
    public async Task GetHostedMaps_MultipleMapsForOneShard_ReturnsAllAscending()
    {
        await InsertAssignmentAsync(77, 310);
        await InsertAssignmentAsync(77, 305);

        var maps = await _repository.GetHostedMapsAsync(77, CancellationToken.None);

        Assert.Equal([(short)305, (short)310], maps);
    }

    private async Task InsertAssignmentAsync(byte shardId, short mapId)
    {
        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();
        await using var command = new SqlCommand(
            "INSERT INTO admin.ShardMapAssignments (ShardId, MapId) VALUES (@ShardId, @MapId);", connection);
        command.Parameters.AddWithValue("ShardId", shardId);
        command.Parameters.AddWithValue("MapId", mapId);
        await command.ExecuteNonQueryAsync();
    }
}
