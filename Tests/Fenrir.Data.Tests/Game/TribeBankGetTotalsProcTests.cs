using CaeriusNet.Abstractions;
using CaeriusNet.Builders;
using Fenrir.Data.Abstractions.Tribes;
using Fenrir.Data.Tests.Fixtures;
using Fenrir.Data.Tribes;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.DependencyInjection;

namespace Fenrir.Data.Tests.Game;

[Collection("SqlServer")]
public class TribeBankGetTotalsProcTests
{
    private readonly string _connectionString;
    private readonly ITribeRepository _tribes;

    public TribeBankGetTotalsProcTests(SqlServerFixture fixture)
    {
        var services = CaeriusNetBuilder
            .Create(new ServiceCollection())
            .WithSqlServer(fixture.ConnectionString)
            .Build();

        var db = services.BuildServiceProvider().GetRequiredService<ICaeriusNetDbContext>();
        _tribes = new TribeRepository(db);
        _connectionString = fixture.ConnectionString;
    }

    [Fact]
    public async Task GetBankTotalsAsync_ReturnsAllFourTribes_SummingOccupiedSlots_AndZeroForATribeNeverDepositedInto()
    {
        await EnsureTribesExistAsync();
        await ClearAllTribeBankRowsAsync();

        await SeedSlotAsync(0, 0, 50_000);
        await SeedSlotAsync(0, 1, 25_000);
        await SeedSlotAsync(1, 3, 10);

        var totals = await _tribes.GetBankTotalsAsync(CancellationToken.None);

        Assert.Equal(4, totals.Count);

        var tribe0 = totals.Single(t => t.TribeId == 0);
        Assert.Equal(75_000L, tribe0.TotalAmount);
        Assert.Equal(2, tribe0.OccupiedSlotCount);

        var tribe1 = totals.Single(t => t.TribeId == 1);
        Assert.Equal(10L, tribe1.TotalAmount);
        Assert.Equal(1, tribe1.OccupiedSlotCount);

        var tribe2 = totals.Single(t => t.TribeId == 2);
        Assert.Equal(0L, tribe2.TotalAmount);
        Assert.Equal(0, tribe2.OccupiedSlotCount);

        var tribe3 = totals.Single(t => t.TribeId == 3);
        Assert.Equal(0L, tribe3.TotalAmount);
        Assert.Equal(0, tribe3.OccupiedSlotCount);
    }

    private async Task EnsureTribesExistAsync()
    {
        for (byte tribeId = 0; tribeId < 4; tribeId++)
            await ExecAsync(
                "IF NOT EXISTS (SELECT 1 FROM game.Tribes WHERE TribeId = @TribeId) " +
                "INSERT INTO game.Tribes (TribeId) VALUES (@TribeId);",
                ("TribeId", tribeId));
    }

    private async Task ClearAllTribeBankRowsAsync()
    {
        await ExecAsync("DELETE FROM game.TribeBank WHERE TribeId IN (0, 1, 2, 3);");
    }

    private async Task SeedSlotAsync(byte tribeId, byte slotIndex, int amount)
    {
        await ExecAsync(
            "INSERT INTO game.TribeBank (TribeId, SlotIndex, Amount) VALUES (@TribeId, @SlotIndex, @Amount);",
            ("TribeId", tribeId), ("SlotIndex", slotIndex), ("Amount", amount));
    }

    private async Task ExecAsync(string sql, params (string Name, object Value)[] parameters)
    {
        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();
        await using var command = new SqlCommand(sql, connection);
        foreach (var (name, value) in parameters)
            command.Parameters.AddWithValue(name, value);
        await command.ExecuteNonQueryAsync();
    }
}
