using System.Security.Cryptography;
using CaeriusNet.Abstractions;
using CaeriusNet.Builders;
using Fenrir.Data.Accounts;
using Fenrir.Data.Characters;
using Fenrir.Data.Tests.Fixtures;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.DependencyInjection;

namespace Fenrir.Data.Tests.Characters;

/// <summary>
///     Phase A3 character persistence against real SQL Server 2025: the extended
///     game.usp_Character_GetForWorldEntry (five result sets via GetWorldEntryBundleAsync),
///     usp_CharacterItems_ReplaceContainer, usp_Character_PersistProgressBatch and usp_Character_AdjustMoney.
///     Rows for the tables that have no write proc yet (skills/hotkeys/buffs/quests -- their writers land with
///     their Phase C verticals) are arranged with direct INSERTs: the point here is the READ contract of the
///     five result sets, and the fixture connection is not bound by the EXECUTE-only application roles.
/// </summary>
[Collection("SqlServer")]
public class CharacterWorldPersistenceTests
{
    private readonly AccountRepository _accounts;
    private readonly CharacterRepository _characters;
    private readonly string _connectionString;

    public CharacterWorldPersistenceTests(SqlServerFixture fixture)
    {
        var services = CaeriusNetBuilder
            .Create(new ServiceCollection())
            .WithSqlServer(fixture.ConnectionString)
            .Build();

        var db = services.BuildServiceProvider().GetRequiredService<ICaeriusNetDbContext>();
        _accounts = new AccountRepository(db);
        _characters = new CharacterRepository(db);
        _connectionString = fixture.ConnectionString;
    }

    [Fact]
    public async Task GetWorldEntryBundleAsync_FreshCharacter_HasA3Defaults_AndNullForAnUnknownId()
    {
        var characterId = await CreateCharacterAsync();

        var bundle = await _characters.GetWorldEntryBundleAsync(characterId, CancellationToken.None);

        Assert.NotNull(bundle);

        // The M1 prefix still comes through the same ordinals...
        Assert.Equal(characterId, bundle.Character.CharacterId);
        Assert.Equal(1, bundle.Character.Level);
        Assert.Equal(0L, bundle.Character.FlushSequence);

        // ...and the appended A3 progression columns land on their table defaults.
        Assert.Equal(0L, bundle.Character.Experience);
        Assert.Equal(1, bundle.Character.Level2); // DF_Characters_Level2
        Assert.Equal(0, bundle.Character.StatPoints);
        Assert.Equal(0L, bundle.Character.Money);
        Assert.Equal(0, bundle.Character.BigMoney);
        Assert.Equal(0, bundle.Character.RebirthCount);
        Assert.Equal(0, bundle.Character.EatLifePotion);
        Assert.Equal(0, bundle.Character.InventoryDate);

        // No game.CharacterQuests row = the all-zeros aQuestInfo of a fresh character (LEFT JOIN + ISNULL).
        Assert.Equal(0, bundle.Character.QuestStepPermanent);
        Assert.Equal(0, bundle.Character.QuestActiveId);
        Assert.Equal(0, bundle.Character.QuestKillCounter);

        Assert.Empty(bundle.Items);
        Assert.Empty(bundle.Skills);
        Assert.Empty(bundle.Hotkeys);
        Assert.Empty(bundle.Buffs);

        var missing = await _characters.GetWorldEntryBundleAsync(-1, CancellationToken.None);
        Assert.Null(missing);
    }

    [Fact]
    public async Task GetWorldEntryBundleAsync_ReturnsEveryResultSet_WithArrangedRows()
    {
        var characterId = await CreateCharacterAsync();
        var itemId = await ScalarAsync<int>("SELECT MIN(ItemId) FROM world.Items;");
        var skillId = await ScalarAsync<int>("SELECT MIN(SkillId) FROM world.Skills;");

        await ExecAsync(
            "INSERT INTO game.CharacterQuests (CharacterId, StepPermanent, ActiveQuestId, QSort, TargetPhase, KillCounter) " +
            $"VALUES ({characterId}, 7, 1, 2, {itemId}, 4);");
        await ExecAsync(
            $"INSERT INTO game.CharacterSkills (CharacterId, SlotIndex, SkillId, Grade) VALUES ({characterId}, 3, {skillId}, 5);");
        await ExecAsync(
            $"INSERT INTO game.CharacterHotkeys (CharacterId, Page, KeyIndex, Sort, Value1, Value2) VALUES ({characterId}, 1, 13, 2, {skillId}, 0);");
        await ExecAsync(
            $"INSERT INTO game.CharacterBuffs (CharacterId, SlotIndex, Value, RemainingLegacyTicks) VALUES ({characterId}, 34, 12, 600);");

        await _characters.ReplaceContainerAsync(characterId, 2,
            [new CharacterItemSlotTvp(12, itemId, 1, 45, 6, 0, 3, 101, 102, 103, 20260101, 777)],
            CancellationToken.None);

        var bundle = await _characters.GetWorldEntryBundleAsync(characterId, CancellationToken.None);

        Assert.NotNull(bundle);

        // RS0's folded quest state (wAvatar.aQuestInfo[5]).
        Assert.Equal(7, bundle.Character.QuestStepPermanent);
        Assert.Equal(1, bundle.Character.QuestActiveId);
        Assert.Equal(2, bundle.Character.QuestSort);
        Assert.Equal(itemId, bundle.Character.QuestTargetPhase);
        Assert.Equal(4, bundle.Character.QuestKillCounter);

        // RS1: every column of the equipment slot round-trips.
        var item = Assert.Single(bundle.Items);
        Assert.Equal(2, item.Container);
        Assert.Equal(12, item.Slot);
        Assert.Equal(itemId, item.ItemId);
        Assert.Equal(1, item.Quantity);
        Assert.Equal(45, item.Enchant);
        Assert.Equal(6, item.Combine);
        Assert.Equal(0, item.Refine);
        Assert.Equal(3, item.Socket);
        Assert.Equal(101, item.SocketGem1);
        Assert.Equal(102, item.SocketGem2);
        Assert.Equal(103, item.SocketGem3);
        Assert.Equal(20260101, item.ExpireDate);
        Assert.Equal(777, item.Serial);

        // RS2/RS3/RS4.
        var skill = Assert.Single(bundle.Skills);
        Assert.Equal(3, skill.SlotIndex);
        Assert.Equal(skillId, skill.SkillId);
        Assert.Equal(5, skill.Grade);

        var hotkey = Assert.Single(bundle.Hotkeys);
        Assert.Equal(1, hotkey.Page);
        Assert.Equal(13, hotkey.KeyIndex);
        Assert.Equal(2, hotkey.Sort);
        Assert.Equal(skillId, hotkey.Value1);

        var buff = Assert.Single(bundle.Buffs);
        Assert.Equal(34, buff.SlotIndex);
        Assert.Equal(12, buff.Value);
        Assert.Equal(600, buff.RemainingLegacyTicks);
    }

    [Fact]
    public async Task ReplaceContainerAsync_ReplacesOnlyTheTargetContainer_AndAnEmptyListClearsIt()
    {
        var characterId = await CreateCharacterAsync();
        var itemId = await ScalarAsync<int>("SELECT MIN(ItemId) FROM world.Items;");

        // Arrange one row in Equipment (container 2) that inventory operations must never touch.
        await _characters.ReplaceContainerAsync(characterId, 2,
            [new CharacterItemSlotTvp(0, itemId, 1, 0, 0, 0, 0, 0, 0, 0, 0, 0)],
            CancellationToken.None);

        // Fill InventoryPage0 (container 0) with two slots...
        await _characters.ReplaceContainerAsync(characterId, 0,
            [
                new CharacterItemSlotTvp(0, itemId, 5, 0, 0, 0, 0, 0, 0, 0, 0, 0),
                new CharacterItemSlotTvp(63, itemId, 1, 0, 0, 0, 0, 0, 0, 0, 0, 1)
            ],
            CancellationToken.None);

        // ...then replace the whole container with a single different slot: old rows must be gone.
        await _characters.ReplaceContainerAsync(characterId, 0,
            [new CharacterItemSlotTvp(7, itemId, 2, 0, 0, 0, 0, 0, 0, 0, 0, 0)],
            CancellationToken.None);

        var afterReplace = await _characters.GetWorldEntryBundleAsync(characterId, CancellationToken.None);
        Assert.NotNull(afterReplace);
        Assert.Equal(2, afterReplace.Items.Count);
        var inventory = Assert.Single(afterReplace.Items, i => i.Container == 0);
        Assert.Equal(7, inventory.Slot);
        Assert.Single(afterReplace.Items, i => i.Container == 2); // equipment untouched

        // Empty list = clear the container (the TVP parameter is omitted, defaulting to empty server-side).
        await _characters.ReplaceContainerAsync(characterId, 0, [], CancellationToken.None);

        var afterClear = await _characters.GetWorldEntryBundleAsync(characterId, CancellationToken.None);
        Assert.NotNull(afterClear);
        var survivor = Assert.Single(afterClear.Items);
        Assert.Equal(2, survivor.Container);
    }

    [Fact]
    public async Task PersistProgressAsync_AppliesANewerFlush_ButIgnoresAStaleReplay_AndAcceptsAnEmptyBatch()
    {
        var characterId = await CreateCharacterAsync();

        await _characters.PersistProgressAsync(
            [new CharacterProgressTvp(characterId, 5, 20, 3, 123456789L, 90, 400, 30, 200, 11, 22, 33, 44, 7, 8, 9)],
            CancellationToken.None);

        var afterFirstFlush = await _characters.GetWorldEntryBundleAsync(characterId, CancellationToken.None);
        Assert.NotNull(afterFirstFlush);
        Assert.Equal(5L, afterFirstFlush.Character.FlushSequence);
        Assert.Equal(20, afterFirstFlush.Character.Level);
        Assert.Equal(3, afterFirstFlush.Character.Level2);
        Assert.Equal(123456789L, afterFirstFlush.Character.Experience);
        Assert.Equal(90, afterFirstFlush.Character.Life);
        Assert.Equal(400, afterFirstFlush.Character.MaxLife);
        Assert.Equal(11, afterFirstFlush.Character.StatVit);
        Assert.Equal(44, afterFirstFlush.Character.StatDex);
        Assert.Equal(7, afterFirstFlush.Character.StatPoints);
        Assert.Equal(8, afterFirstFlush.Character.SkillPoints);
        Assert.Equal(9, afterFirstFlush.Character.ContributionPoints);

        // A stale replay (same FlushSequence, different values) must be discarded silently -- the shared
        // strictly-greater guard, exactly like the position batch.
        await _characters.PersistProgressAsync(
            [new CharacterProgressTvp(characterId, 5, 99, 9, 999L, 1, 1, 1, 1, 0, 0, 0, 0, 0, 0, 0)],
            CancellationToken.None);

        var afterReplay = await _characters.GetWorldEntryBundleAsync(characterId, CancellationToken.None);
        Assert.NotNull(afterReplay);
        Assert.Equal(20, afterReplay.Character.Level);
        Assert.Equal(123456789L, afterReplay.Character.Experience);

        var ex = await Record.ExceptionAsync(() =>
            _characters.PersistProgressAsync(Array.Empty<CharacterProgressTvp>(), CancellationToken.None).AsTask());
        Assert.Null(ex);
    }

    [Fact]
    public async Task AdjustMoneyAsync_CreditsAndDebits_ButRejectsAnOverdraft()
    {
        var characterId = await CreateCharacterAsync();

        await _characters.AdjustMoneyAsync(characterId, 1000L, 2, CancellationToken.None);
        await _characters.AdjustMoneyAsync(characterId, -400L, -1, CancellationToken.None);

        var afterSpend = await _characters.GetWorldEntryBundleAsync(characterId, CancellationToken.None);
        Assert.NotNull(afterSpend);
        Assert.Equal(600L, afterSpend.Character.Money);
        Assert.Equal(1, afterSpend.Character.BigMoney);

        // Overdraft: rejected with THROW 50222, never clamped -- and the balance must be untouched.
        var ex = await Record.ExceptionAsync(() =>
            _characters.AdjustMoneyAsync(characterId, -601L, 0, CancellationToken.None).AsTask());

        Assert.NotNull(ex);
        var sqlException = ex as SqlException ?? ex!.InnerException as SqlException;
        if (sqlException is not null)
            Assert.Equal(50222, sqlException.Number);

        var afterOverdraft = await _characters.GetWorldEntryBundleAsync(characterId, CancellationToken.None);
        Assert.NotNull(afterOverdraft);
        Assert.Equal(600L, afterOverdraft.Character.Money);
        Assert.Equal(1, afterOverdraft.Character.BigMoney);
    }

    [Fact]
    public async Task GetForWorldEntryAsync_TheM1PrefixRead_StillWorksAgainstTheExtendedProc()
    {
        var characterId = await CreateCharacterAsync();

        // The legacy single-DTO read maps the unchanged 19-column prefix of RS0 and ignores the four
        // extra result sets -- existing callers must not have broken.
        var entry = await _characters.GetForWorldEntryAsync(characterId, CancellationToken.None);

        Assert.NotNull(entry);
        Assert.Equal(characterId, entry.CharacterId);
        Assert.Equal(1, entry.Level);
        Assert.Equal(0L, entry.FlushSequence);
    }

    private async Task<int> CreateCharacterAsync()
    {
        var loginName = $"a3test-{Guid.NewGuid():N}";
        var accountId = await _accounts.CreateAsync(loginName, RandomNumberGenerator.GetBytes(32),
            RandomNumberGenerator.GetBytes(16), CancellationToken.None);

        return await _characters.CreateAsync(
            accountId, 0, $"T{Guid.NewGuid():N}"[..8],
            1, 0, 1, 1,
            1, 0f, 0f, 0f,
            100, 100, 50, 50,
            CancellationToken.None);
    }

    private async Task ExecAsync(string sql)
    {
        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();
        await using var command = new SqlCommand(sql, connection);
        await command.ExecuteNonQueryAsync();
    }

    private async Task<T> ScalarAsync<T>(string sql)
    {
        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();
        await using var command = new SqlCommand(sql, connection);
        return (T)(await command.ExecuteScalarAsync())!;
    }
}
