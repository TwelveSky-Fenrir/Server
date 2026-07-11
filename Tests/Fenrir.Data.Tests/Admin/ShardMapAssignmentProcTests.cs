using CaeriusNet.Abstractions;
using CaeriusNet.Builders;
using Fenrir.Data.Abstractions.Admin;
using Fenrir.Data.Admin;
using Fenrir.Data.Tests.Fixtures;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.DependencyInjection;

namespace Fenrir.Data.Tests.Admin;

[Collection("SqlServer")]
public class ShardMapAssignmentProcTests
{
    private readonly string _connectionString;
    private readonly IShardMapAssignmentRepository _repository;

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

        Assert.Equal([305, 310], maps);
    }

    [Fact]
    public async Task GetAllAssignments_IncludesEveryShardRegardlessOfLiveness()
    {
        await InsertAssignmentAsync(78, 320);
        await InsertAssignmentAsync(79, 321);

        var rows = await _repository.GetAllAssignmentsAsync(CancellationToken.None);

        Assert.Contains(rows, row => row.ShardId == 78 && row.MapId == 320);
        Assert.Contains(rows, row => row.ShardId == 79 && row.MapId == 321);
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
