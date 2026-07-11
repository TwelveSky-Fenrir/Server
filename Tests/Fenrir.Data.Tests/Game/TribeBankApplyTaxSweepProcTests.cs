using CaeriusNet.Abstractions;
using CaeriusNet.Builders;
using Fenrir.Data.Abstractions.Tribes;
using Fenrir.Data.Tests.Fixtures;
using Fenrir.Data.Tribes;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.DependencyInjection;

namespace Fenrir.Data.Tests.Game;

// game.usp_TribeBank_ApplyTaxSweep against real SQL Server 2025 -- the durable 10-minute tribe-bank income-tax
// sweep merge (C17 side effect A3): scan-for-first-slot-that-can-absorb, saturating clamp fallback, and
// silent drop when the whole grid is at the 2,000,000,000 ceiling.
[Collection("SqlServer")]
public class TribeBankApplyTaxSweepProcTests
{
    private const long Ceiling = 2_000_000_000L;
    private readonly string _connectionString;
    private readonly ITribeBankSweepRepository _sweeps;

    public TribeBankApplyTaxSweepProcTests(SqlServerFixture fixture)
    {
        var services = CaeriusNetBuilder
            .Create(new ServiceCollection())
            .WithSqlServer(fixture.ConnectionString)
            .Build();

        var db = services.BuildServiceProvider().GetRequiredService<ICaeriusNetDbContext>();
        _sweeps = new TribeBankSweepRepository(db);
        _connectionString = fixture.ConnectionString;
    }

    [Fact]
    public async Task EmptyGrid_DepositsWholeAmountIntoTheFirstSlot()
    {
        await ClearTribeAsync(0);

        await _sweeps.ApplyTaxSweepAsync(5_000, 0, 0, 0, CancellationToken.None);

        Assert.Equal(5_000, await SlotAsync(0, 0));
    }

    [Fact]
    public async Task FirstSlotFull_ScansToTheNextSlotWithRoom()
    {
        await ClearTribeAsync(0);
        await SeedSlotAsync(0, 0, (int)Ceiling); // slot 0 already at the ceiling

        await _sweeps.ApplyTaxSweepAsync(1_000, 0, 0, 0, CancellationToken.None);

        Assert.Equal((int)Ceiling, await SlotAsync(0, 0)); // untouched -- could not absorb
        Assert.Equal(1_000, await SlotAsync(0, 1)); // whole amount landed in the next slot
    }

    [Fact]
    public async Task PartiallyFilledFirstSlot_AbsorbsWhenItFitsUnderTheCeiling()
    {
        await ClearTribeAsync(0);
        await SeedSlotAsync(0, 0, 1_999_999_000);

        await _sweeps.ApplyTaxSweepAsync(500, 0, 0, 0, CancellationToken.None);

        Assert.Equal(1_999_999_500, await SlotAsync(0, 0));
    }

    [Fact]
    public async Task NoSlotCanAbsorbButSomeAreBelowCeiling_ClampsTheFirstBelowCeilingSlotToTheCeiling()
    {
        await ClearTribeAsync(0);
        // Every one of the 50 slots is one short of the ceiling: incoming 100 overflows each, so no slot can
        // absorb the whole amount -- the saturating fallback force-sets the first sub-ceiling slot to ceiling.
        for (byte slot = 0; slot < 50; slot++)
            await SeedSlotAsync(0, slot, 1_999_999_999);

        await _sweeps.ApplyTaxSweepAsync(100, 0, 0, 0, CancellationToken.None);

        Assert.Equal((int)Ceiling, await SlotAsync(0, 0)); // clamped up to the ceiling
        Assert.Equal(1_999_999_999, await SlotAsync(0, 1)); // every later slot left untouched
    }

    [Fact]
    public async Task EverySlotAlreadyAtCeiling_DropsTheAmountSilently()
    {
        await ClearTribeAsync(0);
        for (byte slot = 0; slot < 50; slot++)
            await SeedSlotAsync(0, slot, (int)Ceiling);

        await _sweeps.ApplyTaxSweepAsync(100, 0, 0, 0, CancellationToken.None);

        Assert.Equal((int)Ceiling, await SlotAsync(0, 0)); // nothing placed, no error thrown
    }

    [Fact]
    public async Task MultipleTribes_AreMergedIndependently_ZeroTribesSkipped()
    {
        await ClearTribeAsync(0);
        await ClearTribeAsync(2);

        await _sweeps.ApplyTaxSweepAsync(100, 0, 200, 0, CancellationToken.None);

        Assert.Equal(100, await SlotAsync(0, 0));
        Assert.Equal(200, await SlotAsync(2, 0));
        Assert.Equal(0, await CountRowsAsync(1)); // zero-amount tribe never materialized a row
        Assert.Equal(0, await CountRowsAsync(3));
    }

    private async Task ClearTribeAsync(byte tribeId)
    {
        await ExecAsync("DELETE FROM game.TribeBank WHERE TribeId = @TribeId;",
            ("TribeId", tribeId));
    }

    private async Task SeedSlotAsync(byte tribeId, byte slotIndex, int amount)
    {
        await ExecAsync(
            "INSERT INTO game.TribeBank (TribeId, SlotIndex, Amount) VALUES (@TribeId, @SlotIndex, @Amount);",
            ("TribeId", tribeId), ("SlotIndex", slotIndex), ("Amount", amount));
    }

    private async Task<int> SlotAsync(byte tribeId, byte slotIndex)
    {
        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();
        await using var command = new SqlCommand(
            "SELECT ISNULL((SELECT Amount FROM game.TribeBank WHERE TribeId = @TribeId AND SlotIndex = @SlotIndex), 0);",
            connection);
        command.Parameters.AddWithValue("TribeId", tribeId);
        command.Parameters.AddWithValue("SlotIndex", slotIndex);
        return (int)(await command.ExecuteScalarAsync())!;
    }

    private async Task<int> CountRowsAsync(byte tribeId)
    {
        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();
        await using var command =
            new SqlCommand("SELECT COUNT(*) FROM game.TribeBank WHERE TribeId = @TribeId;", connection);
        command.Parameters.AddWithValue("TribeId", tribeId);
        return (int)(await command.ExecuteScalarAsync())!;
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
