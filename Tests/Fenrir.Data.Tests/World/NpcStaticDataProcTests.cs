using CaeriusNet.Abstractions;
using CaeriusNet.Builders;
using Fenrir.Data.Abstractions.World;
using Fenrir.Data.Tests.Fixtures;
using Fenrir.Data.World;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.DependencyInjection;

namespace Fenrir.Data.Tests.World;

[Collection("SqlServer")]
public class NpcStaticDataProcTests
{
    private static int _nextNpcId = 900_001;

    private readonly string _connectionString;
    private readonly IWorldDataRepository _repository;

    public NpcStaticDataProcTests(SqlServerFixture fixture)
    {
        var services = CaeriusNetBuilder
            .Create(new ServiceCollection())
            .WithSqlServer(fixture.ConnectionString)
            .Build();

        var db = services.BuildServiceProvider().GetRequiredService<ICaeriusNetDbContext>();
        _repository = new WorldDataRepository(db);
        _connectionString = fixture.ConnectionString;
    }

    [Fact]
    public async Task GetNpcsAsync_LoadsSeededCatalog_AllRowsWithinTheNewCheckConstraintBounds()
    {
        var npcs = (await _repository.GetNpcsAsync(CancellationToken.None))
            .Where(n => n.NpcId <= 500).ToList();
        var menuOptions = (await _repository.GetNpcMenuOptionsAsync(CancellationToken.None))
            .Where(o => o.NpcId <= 500).ToList();
        var gambleCosts = (await _repository.GetNpcGambleCostsAsync(CancellationToken.None))
            .Where(g => g.NpcId <= 500).ToList();

        Assert.Equal(131, npcs.Count);
        Assert.Equal(13_100, menuOptions.Count);
        Assert.Equal(13_050, gambleCosts.Count);

        foreach (var npc in npcs)
        {
            Assert.InRange(npc.Tribe, (byte)1, (byte)5);
            Assert.InRange(npc.Type, (byte)1, (byte)17);
            Assert.InRange(npc.DataSortNumber2D, 1, 10_000);
            Assert.InRange(npc.DataSortNumber3D, 1, 10_000);
            Assert.InRange(npc.Size1, 1, 1_000);
            Assert.InRange(npc.Size2, 1, 1_000);
            Assert.InRange(npc.Size3, 1, 1_000);
        }

        foreach (var option in menuOptions)
            Assert.True(option.OptionId is 1 or 2,
                $"NpcId {option.NpcId} SlotIndex {option.SlotIndex} has OptionId {option.OptionId}, " +
                "which CK_NpcMenuOptions_OptionId should never allow through.");

        foreach (var gambleCost in gambleCosts)
            Assert.InRange(gambleCost.Value, 0, 100_000_000);
    }

    [Theory]
    [InlineData((byte)0)]
    [InlineData((byte)6)]
    public async Task InsertNpc_TribeOutOfRange_ThrowsCheckConstraintViolation(byte tribe)
    {
        var ex = await Record.ExceptionAsync(() => InsertNpcAsync(NewNpcId(), tribe));

        AssertCheckConstraintViolation(ex);
    }

    [Theory]
    [InlineData((byte)0)]
    [InlineData((byte)18)]
    public async Task InsertNpc_TypeOutOfRange_ThrowsCheckConstraintViolation(byte type)
    {
        var ex = await Record.ExceptionAsync(() => InsertNpcAsync(NewNpcId(), type: type));

        AssertCheckConstraintViolation(ex);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(10_001)]
    public async Task InsertNpc_DataSortNumber2DOutOfRange_ThrowsCheckConstraintViolation(int dataSortNumber2D)
    {
        var ex = await Record.ExceptionAsync(() => InsertNpcAsync(NewNpcId(), dataSortNumber2D: dataSortNumber2D));

        AssertCheckConstraintViolation(ex);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(10_001)]
    public async Task InsertNpc_DataSortNumber3DOutOfRange_ThrowsCheckConstraintViolation(int dataSortNumber3D)
    {
        var ex = await Record.ExceptionAsync(() => InsertNpcAsync(NewNpcId(), dataSortNumber3D: dataSortNumber3D));

        AssertCheckConstraintViolation(ex);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1_001)]
    public async Task InsertNpc_Size1OutOfRange_ThrowsCheckConstraintViolation(int size1)
    {
        var ex = await Record.ExceptionAsync(() => InsertNpcAsync(NewNpcId(), size1: size1));

        AssertCheckConstraintViolation(ex);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1_001)]
    public async Task InsertNpc_Size2OutOfRange_ThrowsCheckConstraintViolation(int size2)
    {
        var ex = await Record.ExceptionAsync(() => InsertNpcAsync(NewNpcId(), size2: size2));

        AssertCheckConstraintViolation(ex);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1_001)]
    public async Task InsertNpc_Size3OutOfRange_ThrowsCheckConstraintViolation(int size3)
    {
        var ex = await Record.ExceptionAsync(() => InsertNpcAsync(NewNpcId(), size3: size3));

        AssertCheckConstraintViolation(ex);
    }

    [Fact]
    public async Task InsertNpc_ValidRowAtLowerBounds_Succeeds()
    {
        var ex = await Record.ExceptionAsync(() => InsertNpcAsync(NewNpcId()));

        Assert.Null(ex);
    }

    [Fact]
    public async Task InsertNpc_ValidRowAtUpperBounds_Succeeds()
    {
        var ex = await Record.ExceptionAsync(() => InsertNpcAsync(
            NewNpcId(), 5, 17, 10_000, 10_000, 1_000, 1_000, 1_000));

        Assert.Null(ex);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(3)]
    public async Task InsertNpcMenuOption_OptionIdNotOneOrTwo_ThrowsCheckConstraintViolation(int optionId)
    {
        var npcId = NewNpcId();
        await InsertNpcAsync(npcId);

        var ex = await Record.ExceptionAsync(() => InsertNpcMenuOptionAsync(npcId, 0, optionId));

        AssertCheckConstraintViolation(ex);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    public async Task InsertNpcMenuOption_ValidOptionId_Succeeds(int optionId)
    {
        var npcId = NewNpcId();
        await InsertNpcAsync(npcId);

        var ex = await Record.ExceptionAsync(() => InsertNpcMenuOptionAsync(npcId, 0, optionId));

        Assert.Null(ex);
    }

    [Fact]
    public async Task InsertNpcGambleCost_ValueNegative_ThrowsCheckConstraintViolation()
    {
        var npcId = NewNpcId();
        await InsertNpcAsync(npcId);

        var ex = await Record.ExceptionAsync(() => InsertNpcGambleCostAsync(npcId, value: -1));

        AssertCheckConstraintViolation(ex);
    }

    [Fact]
    public async Task InsertNpcGambleCost_ValueOverMax_ThrowsCheckConstraintViolation()
    {
        var npcId = NewNpcId();
        await InsertNpcAsync(npcId);

        var ex = await Record.ExceptionAsync(() => InsertNpcGambleCostAsync(npcId, value: 100_000_001));

        AssertCheckConstraintViolation(ex);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(100_000_000)]
    public async Task InsertNpcGambleCost_ValidValueAtBounds_Succeeds(int value)
    {
        var npcId = NewNpcId();
        await InsertNpcAsync(npcId);

        var ex = await Record.ExceptionAsync(() => InsertNpcGambleCostAsync(npcId, value: value));

        Assert.Null(ex);
    }

    private static int NewNpcId()
    {
        return Interlocked.Increment(ref _nextNpcId);
    }

    private static void AssertCheckConstraintViolation(Exception? ex)
    {
        Assert.NotNull(ex);
        var sqlException = ex as SqlException ?? ex.InnerException as SqlException;
        Assert.NotNull(sqlException);
        Assert.Equal(547, sqlException!.Number);
    }

    private async Task InsertNpcAsync(
        int npcId,
        byte tribe = 1,
        byte type = 1,
        int dataSortNumber2D = 1,
        int dataSortNumber3D = 1,
        int size1 = 1,
        int size2 = 1,
        int size3 = 1)
    {
        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();
        await using var command = new SqlCommand(
            """
            INSERT INTO world.Npcs
                (NpcId, Name, Tribe, Type, DataSortNumber2D, DataSortNumber3D, Size1, Size2, Size3)
            VALUES
                (@NpcId, @Name, @Tribe, @Type, @DataSortNumber2D, @DataSortNumber3D, @Size1, @Size2, @Size3);
            """, connection);
        command.Parameters.AddWithValue("NpcId", npcId);
        command.Parameters.AddWithValue("Name", $"Test{npcId}");
        command.Parameters.AddWithValue("Tribe", tribe);
        command.Parameters.AddWithValue("Type", type);
        command.Parameters.AddWithValue("DataSortNumber2D", dataSortNumber2D);
        command.Parameters.AddWithValue("DataSortNumber3D", dataSortNumber3D);
        command.Parameters.AddWithValue("Size1", size1);
        command.Parameters.AddWithValue("Size2", size2);
        command.Parameters.AddWithValue("Size3", size3);
        await command.ExecuteNonQueryAsync();
    }

    private async Task InsertNpcMenuOptionAsync(int npcId, short slotIndex, int optionId)
    {
        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();
        await using var command = new SqlCommand(
            """
            INSERT INTO world.NpcMenuOptions (NpcId, SlotIndex, OptionId)
            VALUES (@NpcId, @SlotIndex, @OptionId);
            """, connection);
        command.Parameters.AddWithValue("NpcId", npcId);
        command.Parameters.AddWithValue("SlotIndex", slotIndex);
        command.Parameters.AddWithValue("OptionId", optionId);
        await command.ExecuteNonQueryAsync();
    }

    private async Task InsertNpcGambleCostAsync(int npcId, short gambleTier = 0, byte costIndex = 0, int value = 0)
    {
        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();
        await using var command = new SqlCommand(
            """
            INSERT INTO world.NpcGambleCosts (NpcId, GambleTier, CostIndex, Value)
            VALUES (@NpcId, @GambleTier, @CostIndex, @Value);
            """, connection);
        command.Parameters.AddWithValue("NpcId", npcId);
        command.Parameters.AddWithValue("GambleTier", gambleTier);
        command.Parameters.AddWithValue("CostIndex", costIndex);
        command.Parameters.AddWithValue("Value", value);
        await command.ExecuteNonQueryAsync();
    }
}
