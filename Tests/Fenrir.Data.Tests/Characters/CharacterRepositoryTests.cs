using System.Security.Cryptography;
using CaeriusNet.Abstractions;
using CaeriusNet.Builders;
using Fenrir.Data.Abstractions.Accounts;
using Fenrir.Data.Abstractions.Characters;
using Fenrir.Data.Accounts;
using Fenrir.Data.Characters;
using Fenrir.Data.Social;
using Fenrir.Data.Tests.Fixtures;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.DependencyInjection;

namespace Fenrir.Data.Tests.Characters;

[Collection("SqlServer")]
public class CharacterRepositoryTests
{
    private readonly IAccountRepository _accounts;
    private readonly ICharacterRepository _characters;
    private readonly string _connectionString;
    private readonly MentorRepository _mentors;

    public CharacterRepositoryTests(SqlServerFixture fixture)
    {
        var services = CaeriusNetBuilder
            .Create(new ServiceCollection())
            .WithSqlServer(fixture.ConnectionString)
            .Build();

        var db = services.BuildServiceProvider().GetRequiredService<ICaeriusNetDbContext>();
        _accounts = new AccountRepository(db);
        _characters = new CharacterRepository(db);
        _mentors = new MentorRepository(db);
        _connectionString = fixture.ConnectionString;
    }

    [Fact]
    public async Task CreateAsync_CreatesCharacterInSlot0_AndGetByAccountFindsIt()
    {
        var accountId = await CreateTestAccountAsync();
        var name = NewCharacterName();

        var characterId = await CreateCharacterAsync(accountId, 0, name);

        Assert.True(characterId > 0);

        var roster = await _characters.GetByAccountAsync(accountId, CancellationToken.None);

        var row = Assert.Single(roster);
        Assert.Equal(characterId, row.CharacterId);
        Assert.Equal(0, row.Slot);
        Assert.Equal(name, row.Name);
        Assert.Equal(1, row.Tribe);
        Assert.Equal(0, row.Gender);
        Assert.Equal(1, row.HeadType);
        Assert.Equal(1, row.FaceType);
        Assert.Equal(1, row.Level);
    }

    [Fact]
    public async Task CreateAsync_SameSlotTwice_Throws_ButADifferentSlotSucceeds()
    {
        var accountId = await CreateTestAccountAsync();
        await CreateCharacterAsync(accountId, 0);

        var ex = await Record.ExceptionAsync(() => CreateCharacterAsync(accountId, 0));

        Assert.NotNull(ex);

        var sqlException = ex as SqlException ?? ex!.InnerException as SqlException;
        if (sqlException is not null)
            Assert.Equal(50201, sqlException.Number);

        var secondCharacterId = await CreateCharacterAsync(accountId, 1);
        Assert.True(secondCharacterId > 0);
    }

    [Fact]
    public async Task CreateAsync_NameAlreadyTaken_Throws_EvenForADifferentAccount()
    {
        var accountA = await CreateTestAccountAsync();
        var accountB = await CreateTestAccountAsync();
        var name = NewCharacterName();

        await CreateCharacterAsync(accountA, 0, name);

        var ex = await Record.ExceptionAsync(() => CreateCharacterAsync(accountB, 0, name));

        Assert.NotNull(ex);

        var sqlException = ex as SqlException ?? ex!.InnerException as SqlException;
        if (sqlException is not null)
            Assert.Equal(50202, sqlException.Number);
    }

    [Fact]
    public async Task DeleteAsync_RemovesTheCharacter_AndIsIdempotentOnAnAlreadyEmptySlot()
    {
        var accountId = await CreateTestAccountAsync();
        await CreateCharacterAsync(accountId, 0);

        await _characters.DeleteAsync(accountId, 0, CancellationToken.None);

        var roster = await _characters.GetByAccountAsync(accountId, CancellationToken.None);
        Assert.Empty(roster);

        var ex = await Record.ExceptionAsync(() =>
            _characters.DeleteAsync(accountId, 0, CancellationToken.None).AsTask());
        Assert.Null(ex);
    }

    [Fact]
    public async Task DeleteAsync_WithEveryNormalizedChildRowPopulated_CleansUpAllOfThem_WithoutAnFkViolation()
    {
        var accountId = await CreateTestAccountAsync();
        var name = NewCharacterName();

        var friendId = await CreateCharacterAsync(await CreateTestAccountAsync(), 0);
        var studentId = await CreateCharacterAsync(await CreateTestAccountAsync(), 0);

        var characterId = await _characters.CreateWithStarterKitAsync(
            accountId, 0, name, 0, 0, 1, 1,
            1, 0f, 0f, 0f,
            100, 100, 50, 50,
            0, 0,
            [new CharacterItemSlotTvp(2, 8, 1, 0, 0, 0, 0, 0, 0, 0, 0, 0)],
            [new CharacterItemSlotTvp(0, 1026, 999, 0, 0, 0, 0, 0, 0, 0, 0, 0)],
            [new CharacterSkillSlotTvp(0, 1, 1)],
            [new CharacterHotkeySlotTvp(0, 0, 1, 1, 1)],
            CancellationToken.None);

        await ExecAsync($"""
                         INSERT INTO game.CharacterBuffs (CharacterId, SlotIndex, Value, RemainingLegacyTicks)
                         VALUES ({characterId}, 0, 5, 100);
                         """);
        await ExecAsync($"""
                         INSERT INTO game.CharacterQuests (CharacterId, StepPermanent, ActiveQuestId, QSort, TargetPhase, KillCounter)
                         VALUES ({characterId}, 1, 2, 3, 4, 5);
                         """);
        await ExecAsync($"""
                         INSERT INTO game.CharacterFriends (CharacterId, Slot, FriendCharacterId)
                         VALUES ({characterId}, 0, {friendId});
                         """);
        await ExecAsync($"""
                         INSERT INTO game.HeroRankings (CharacterId, PeriodKind, Points)
                         VALUES ({characterId}, 0, 100);
                         """);
        await ExecAsync($"""
                         INSERT INTO game.OfflineShops (CharacterId, ZoneNumber, ShopState, Money, BigMoney)
                         VALUES ({characterId}, NULL, 0, 0, 0);
                         """);

        await _mentors.BondAsync(characterId, studentId, CancellationToken.None);

        var deleteEx = await Record.ExceptionAsync(() =>
            _characters.DeleteAsync(accountId, 0, CancellationToken.None).AsTask());
        Assert.Null(deleteEx);

        Assert.Empty(await _characters.GetByAccountAsync(accountId, CancellationToken.None));

        Assert.Equal(0,
            await ScalarAsync<int>($"SELECT COUNT(*) FROM game.CharacterItems WHERE CharacterId = {characterId};"));
        Assert.Equal(0,
            await ScalarAsync<int>($"SELECT COUNT(*) FROM game.CharacterSkills WHERE CharacterId = {characterId};"));
        Assert.Equal(0,
            await ScalarAsync<int>($"SELECT COUNT(*) FROM game.CharacterHotkeys WHERE CharacterId = {characterId};"));
        Assert.Equal(0,
            await ScalarAsync<int>($"SELECT COUNT(*) FROM game.CharacterBuffs WHERE CharacterId = {characterId};"));
        Assert.Equal(0,
            await ScalarAsync<int>($"SELECT COUNT(*) FROM game.CharacterQuests WHERE CharacterId = {characterId};"));
        Assert.Equal(0, await ScalarAsync<int>(
            $"SELECT COUNT(*) FROM game.CharacterFriends WHERE CharacterId = {characterId} OR FriendCharacterId = {characterId};"));
        Assert.Equal(0,
            await ScalarAsync<int>($"SELECT COUNT(*) FROM game.HeroRankings WHERE CharacterId = {characterId};"));
        Assert.Equal(0,
            await ScalarAsync<int>($"SELECT COUNT(*) FROM game.OfflineShops WHERE CharacterId = {characterId};"));
        Assert.Equal(0,
            await ScalarAsync<int>($"SELECT COUNT(*) FROM game.OfflineShopItems WHERE CharacterId = {characterId};"));

        var studentTeacherId = await ScalarAsync<object>(
            $"SELECT TeacherCharacterId FROM game.Characters WHERE CharacterId = {studentId};");
        Assert.Equal(DBNull.Value, studentTeacherId);
    }

    [Fact]
    public async Task GetForWorldEntryAsync_ReturnsEveryColumn_AndNullForAnUnknownCharacterId()
    {
        var accountId = await CreateTestAccountAsync();
        var name = NewCharacterName();

        var characterId = await _characters.CreateAsync(
            accountId, 2, name,
            3, 1, 2, 4,
            7, 11.5f, 22.5f, 33.5f,
            250, 300, 40, 60,
            CancellationToken.None);

        var entry = await _characters.GetForWorldEntryAsync(characterId, CancellationToken.None);

        Assert.NotNull(entry);
        Assert.Equal(characterId, entry.CharacterId);
        Assert.Equal(accountId, entry.AccountId);
        Assert.Equal(2, entry.Slot);
        Assert.Equal(name, entry.Name);
        Assert.Equal(3, entry.Tribe);
        Assert.Equal(1, entry.Gender);
        Assert.Equal(2, entry.HeadType);
        Assert.Equal(4, entry.FaceType);
        Assert.Equal(1, entry.Level);
        Assert.Equal(7, entry.MapId);
        Assert.Equal(11.5f, entry.PosX);
        Assert.Equal(22.5f, entry.PosY);
        Assert.Equal(33.5f, entry.PosZ);
        Assert.Equal(0f, entry.Heading);
        Assert.Equal(250, entry.Life);
        Assert.Equal(300, entry.MaxLife);
        Assert.Equal(40, entry.Mana);
        Assert.Equal(60, entry.MaxMana);
        Assert.Equal(0L, entry.FlushSequence);

        var missing = await _characters.GetForWorldEntryAsync(-1, CancellationToken.None);
        Assert.Null(missing);
    }

    [Fact]
    public async Task PersistPositionsAsync_AppliesANewerFlush_ButIgnoresAStaleReplay_AndAcceptsAnEmptyBatch()
    {
        var accountId = await CreateTestAccountAsync();
        var characterId = await CreateCharacterAsync(accountId, 0);

        await _characters.PersistPositionsAsync(
            [new CharacterPositionTvp(characterId, 5, 9, 111f, 222f, 333f, 1.5f)],
            CancellationToken.None);

        var afterFirstFlush = await _characters.GetForWorldEntryAsync(characterId, CancellationToken.None);
        Assert.NotNull(afterFirstFlush);
        Assert.Equal(5L, afterFirstFlush.FlushSequence);
        Assert.Equal(9, afterFirstFlush.MapId);
        Assert.Equal(111f, afterFirstFlush.PosX);
        Assert.Equal(222f, afterFirstFlush.PosY);
        Assert.Equal(333f, afterFirstFlush.PosZ);
        Assert.Equal(1.5f, afterFirstFlush.Heading);

        await _characters.PersistPositionsAsync(
            [new CharacterPositionTvp(characterId, 5, 99, 999f, 999f, 999f, 9f)],
            CancellationToken.None);

        var afterReplay = await _characters.GetForWorldEntryAsync(characterId, CancellationToken.None);
        Assert.NotNull(afterReplay);
        Assert.Equal(5L, afterReplay.FlushSequence);
        Assert.Equal(9, afterReplay.MapId);
        Assert.Equal(111f, afterReplay.PosX);
        Assert.Equal(222f, afterReplay.PosY);
        Assert.Equal(333f, afterReplay.PosZ);
        Assert.Equal(1.5f, afterReplay.Heading);

        var ex = await Record.ExceptionAsync(() =>
            _characters.PersistPositionsAsync(Array.Empty<CharacterPositionTvp>(), CancellationToken.None).AsTask());
        Assert.Null(ex);
    }

    [Fact]
    public async Task GrantTribeTransferPermitAsync_AccumulatesAcrossCalls_AndRejectsGoingNegative()
    {
        var accountId = await CreateTestAccountAsync();
        var characterId = await CreateCharacterAsync(accountId, 0);

        var afterFirst = await _characters.GrantTribeTransferPermitAsync(characterId, 1, CancellationToken.None);
        Assert.Equal(1, afterFirst);

        var afterSecond = await _characters.GrantTribeTransferPermitAsync(characterId, 1, CancellationToken.None);
        Assert.Equal(2, afterSecond);

        var afterSpend = await _characters.GrantTribeTransferPermitAsync(characterId, -2, CancellationToken.None);
        Assert.Equal(0, afterSpend);

        var overspend = await Record.ExceptionAsync(() =>
            _characters.GrantTribeTransferPermitAsync(characterId, -1, CancellationToken.None).AsTask());
        Assert.Equal(50312, Assert.IsType<SqlException>(overspend!.InnerException).Number);

        var unknownCharacter = await Record.ExceptionAsync(() =>
            _characters.GrantTribeTransferPermitAsync(-1, 1, CancellationToken.None).AsTask());
        Assert.Equal(50312, Assert.IsType<SqlException>(unknownCharacter!.InnerException).Number);
    }

    [Fact]
    public async Task AdjustDeathProtectionAsync_DecrementsShieldCharges_AndRejectsGoingNegative()
    {
        var accountId = await CreateTestAccountAsync();
        var characterId = await CreateCharacterAsync(accountId, 0);

        var afterGrant = await _characters.AdjustDeathProtectionAsync(characterId, 5, CancellationToken.None);
        Assert.Equal(5, afterGrant);

        var afterFirstConsume = await _characters.AdjustDeathProtectionAsync(characterId, -1, CancellationToken.None);
        Assert.Equal(4, afterFirstConsume);

        var afterDraining = await _characters.AdjustDeathProtectionAsync(characterId, -4, CancellationToken.None);
        Assert.Equal(0, afterDraining);

        var overspend = await Record.ExceptionAsync(() =>
            _characters.AdjustDeathProtectionAsync(characterId, -1, CancellationToken.None).AsTask());
        Assert.Equal(50332, Assert.IsType<SqlException>(overspend!.InnerException).Number);

        var unknownCharacter = await Record.ExceptionAsync(() =>
            _characters.AdjustDeathProtectionAsync(-1, 1, CancellationToken.None).AsTask());
        Assert.Equal(50332, Assert.IsType<SqlException>(unknownCharacter!.InnerException).Number);
    }

    [Fact]
    public async Task ReplaceContainerAsync_AcceptsQuantityExactlyAtThe999Cap_ButTheCheckConstraintRejectsOverCap()
    {
        var accountId = await CreateTestAccountAsync();
        var characterId = await CreateCharacterAsync(accountId, 0);

        await _characters.ReplaceContainerAsync(characterId, 0,
            [new CharacterItemSlotTvp(0, 1026, 999, 0, 0, 0, 0, 0, 0, 0, 0, 0)],
            CancellationToken.None);

        var atCap = await ScalarAsync<int>(
            $"SELECT Quantity FROM game.CharacterItems WHERE CharacterId = {characterId} AND Container = 0 AND Slot = 0;");
        Assert.Equal(999, atCap);

        var overCap = await Record.ExceptionAsync(() =>
            _characters.ReplaceContainerAsync(characterId, 1,
                [new CharacterItemSlotTvp(0, 1026, 1000, 0, 0, 0, 0, 0, 0, 0, 0, 0)],
                CancellationToken.None).AsTask());
        Assert.Equal(547, Assert.IsType<SqlException>(overCap!.InnerException).Number);
    }

    [Fact]
    public async Task GetAccountRosterAsync_AfterCreateWithStarterKit_ReturnsTheRealEquippedWeaponAndTorsoArmor()
    {
        var accountId = await CreateTestAccountAsync();
        var name = NewCharacterName();

        const int weaponItemId = 84527;
        const int torsoItemId = 84575;

        var characterId = await _characters.CreateWithStarterKitAsync(
            accountId, 0, name, 0, 1, 2, 1,
            1, 6f, 0f, -7f,
            30, 100, 21, 50,
            20260101, 0L,
            [
                new CharacterItemSlotTvp(7, weaponItemId, 1, 0, 0, 0, 0, 0, 0, 0, 0, 0),
                new CharacterItemSlotTvp(2, torsoItemId, 1, 0, 0, 0, 0, 0, 0, 0, 0, 0)
            ],
            [],
            [],
            [],
            CancellationToken.None);

        var narrowRoster = await _characters.GetByAccountAsync(accountId, CancellationToken.None);
        Assert.Single(narrowRoster);

        var roster = await _characters.GetAccountRosterAsync(accountId, CancellationToken.None);

        var character = Assert.Single(roster.Characters);
        Assert.Equal(characterId, character.CharacterId);
        Assert.Equal((byte)0, character.Slot);
        Assert.Equal(name, character.Name);
        Assert.Equal((byte)0, character.Tribe);
        Assert.Equal((byte)1, character.Gender);
        Assert.Equal((byte)2, character.HeadType);
        Assert.Equal((byte)1, character.FaceType);
        Assert.Equal(1, character.Level);

        Assert.Equal(2, roster.Items.Count);
        var weaponRow = Assert.Single(roster.Items, i => i.Slot == 7);
        var torsoRow = Assert.Single(roster.Items, i => i.Slot == 2);
        Assert.Equal(characterId, weaponRow.CharacterId);
        Assert.Equal((byte)2, weaponRow.Container);
        Assert.Equal(weaponItemId, weaponRow.ItemId);
        Assert.Equal(characterId, torsoRow.CharacterId);
        Assert.Equal((byte)2, torsoRow.Container);
        Assert.Equal(torsoItemId, torsoRow.ItemId);
    }

    [Fact]
    public async Task GetAccountRosterAsync_AccountWithNoCharactersYet_ReturnsEmptyCharactersAndItems()
    {
        var accountId = await CreateTestAccountAsync();

        var roster = await _characters.GetAccountRosterAsync(accountId, CancellationToken.None);

        Assert.Empty(roster.Characters);
        Assert.Empty(roster.Items);
    }

    private async Task<int> CreateTestAccountAsync()
    {
        var loginName = $"chartest-{Guid.NewGuid():N}";
        return await _accounts.CreateAsync(loginName, RandomNumberGenerator.GetBytes(32),
            RandomNumberGenerator.GetBytes(16), CancellationToken.None);
    }

    private Task<int> CreateCharacterAsync(int accountId, byte slot, string? name = null)
    {
        return _characters.CreateAsync(
            accountId, slot, name ?? NewCharacterName(),
            1, 0, 1, 1,
            1, 0f, 0f, 0f,
            100, 100, 50, 50,
            CancellationToken.None).AsTask();
    }

    private static string NewCharacterName()
    {
        return $"T{Guid.NewGuid():N}"[..8];
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
