using System.Security.Cryptography;
using CaeriusNet.Abstractions;
using CaeriusNet.Builders;
using Fenrir.Data.Accounts;
using Fenrir.Data.Characters;
using Fenrir.Data.Tests.Fixtures;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.DependencyInjection;

namespace Fenrir.Data.Tests.Characters;

// op17's DB layer: world.usp_StarterKit_GetByPreviousTribe (the read side) and
// game.usp_Character_CreateWithStarterKit (the write side), against real SQL Server 2025.
[Collection("SqlServer")]
public class StarterKitProcTests
{
    private readonly IAccountRepository _accounts;
    private readonly ICharacterRepository _characters;
    private readonly IStarterKitRepository _starterKits;
    private readonly string _connectionString;

    public StarterKitProcTests(SqlServerFixture fixture)
    {
        var services = CaeriusNetBuilder
            .Create(new ServiceCollection())
            .WithSqlServer(fixture.ConnectionString)
            .Build();

        var db = services.BuildServiceProvider().GetRequiredService<ICaeriusNetDbContext>();
        _accounts = new AccountRepository(db);
        _characters = new CharacterRepository(db);
        _starterKits = new StarterKitRepository(db);
        _connectionString = fixture.ConnectionString;
    }

    [Fact]
    public async Task GetByPreviousTribeAsync_NobleDragon_ReturnsTheSeededCatalogAndZone1sSpawn()
    {
        var bundle = await _starterKits.GetByPreviousTribeAsync(0, 1, CancellationToken.None);

        Assert.Equal(6, bundle.Equipment.Count); // Armor+Gloves+Boots + 3 weapon alternatives
        Assert.Contains(bundle.Equipment, e => e is { EquipSlot: 2, ItemId: 8 });
        Assert.Contains(bundle.Equipment, e => e is { EquipSlot: 3, ItemId: 9 });
        Assert.Contains(bundle.Equipment, e => e is { EquipSlot: 5, ItemId: 10 });
        Assert.Contains(bundle.Equipment, e => e is { EquipSlot: 7, ItemId: 5 });
        Assert.Contains(bundle.Equipment, e => e is { EquipSlot: 7, ItemId: 6 });
        Assert.Contains(bundle.Equipment, e => e is { EquipSlot: 7, ItemId: 7 });

        Assert.Equal(4, bundle.Inventory.Count);
        Assert.Contains(bundle.Inventory, i => i is { SlotIndex: 0, ItemId: 1026, Quantity: 999 });
        Assert.Contains(bundle.Inventory, i => i is { SlotIndex: 3, ItemId: 1001, Quantity: 10 });

        Assert.Equal(30, bundle.Skills.Count);
        Assert.Contains(bundle.Skills, s => s is { SlotIndex: 0, SkillId: 1, Grade: 1 });
        Assert.Contains(bundle.Skills, s => s is { SlotIndex: 34, SkillId: 80, Grade: 1 });
        Assert.DoesNotContain(bundle.Skills, s => s.SlotIndex == 7); // empty slot, no row

        Assert.Equal(3, bundle.Hotkeys.Count);
        Assert.Contains(bundle.Hotkeys, h => h is { Page: 0, KeyIndex: 0, Sort: 1, Value1: 1, Value2: 1 });
        Assert.Contains(bundle.Hotkeys, h => h is { Page: 0, KeyIndex: 1, Sort: 34, Value1: 999, Value2: 3 });

        Assert.NotNull(bundle.Spawn);
        Assert.Equal(6f, bundle.Spawn!.PosX);
        Assert.Equal(0f, bundle.Spawn.PosY);
        Assert.Equal(-7f, bundle.Spawn.PosZ);
    }

    [Fact]
    public async Task GetByPreviousTribeAsync_RoyalSerpent_ReturnsItsOwnCatalogAndZone6sSpawn()
    {
        var bundle = await _starterKits.GetByPreviousTribeAsync(1, 6, CancellationToken.None);

        Assert.Contains(bundle.Equipment, e => e is { EquipSlot: 2, ItemId: 14 });
        Assert.Contains(bundle.Equipment, e => e is { EquipSlot: 7, ItemId: 11 });
        Assert.Contains(bundle.Skills, s => s is { SlotIndex: 0, SkillId: 20, Grade: 1 });
        Assert.Contains(bundle.Hotkeys, h => h is { Page: 0, KeyIndex: 0, Sort: 20 });

        Assert.NotNull(bundle.Spawn);
        Assert.Equal(-190f, bundle.Spawn!.PosX);
        Assert.Equal(1270f, bundle.Spawn.PosZ);
    }

    [Fact]
    public async Task GetByPreviousTribeAsync_UnseededMapId_LeavesSpawnNull()
    {
        var bundle = await _starterKits.GetByPreviousTribeAsync(0, short.MaxValue, CancellationToken.None);

        Assert.Null(bundle.Spawn);
    }

    [Fact]
    public async Task CreateWithStarterKitAsync_PersistsStatsPetBuffsPremium_AndEveryCatalogRow()
    {
        var accountId = await CreateTestAccountAsync();
        var name = NewCharacterName();
        var welcomeBuffUntilDate = 20260712; // arbitrary YYYYMMDD, opaque to the proc
        var premiumUntilUnixSeconds = 1_800_000_000L;

        List<CharacterItemSlotTvp> equipment =
        [
            new CharacterItemSlotTvp(2, 8, 1, 0, 0, 0, 0, 0, 0, 0, 0, 0),
            new CharacterItemSlotTvp(7, 6, 1, 0, 0, 0, 0, 0, 0, 0, 0, 0),
            new CharacterItemSlotTvp(1, 1407, 1, 40, 0, 0, 0, 0, 0, 0, 0, 0)
        ];
        List<CharacterItemSlotTvp> inventory = [new CharacterItemSlotTvp(0, 1026, 999, 0, 0, 0, 0, 0, 0, 0, 0, 0)];
        List<CharacterSkillSlotTvp> skills = [new CharacterSkillSlotTvp(0, 1, 1), new CharacterSkillSlotTvp(1, 2, 1)];
        List<CharacterHotkeySlotTvp> hotkeys = [new CharacterHotkeySlotTvp(0, 0, 1, 1, 1)];

        var characterId = await _characters.CreateWithStarterKitAsync(
            accountId, 0, name, 0, 1, 2, 1,
            1, 6f, 0f, -7f,
            100, 100, 50, 50,
            welcomeBuffUntilDate, premiumUntilUnixSeconds,
            equipment, inventory, skills, hotkeys,
            CancellationToken.None);

        Assert.True(characterId > 0);

        var bundle = await _characters.GetWorldEntryBundleAsync(characterId, CancellationToken.None);
        Assert.NotNull(bundle);

        Assert.Equal(1, bundle.Character.StatVit);
        Assert.Equal(1, bundle.Character.StatStr);
        Assert.Equal(1, bundle.Character.StatInt);
        Assert.Equal(1, bundle.Character.StatDex);
        Assert.Equal(640000000, bundle.Character.PetGrowth);
        Assert.Equal((byte)100, bundle.Character.PetActivity);
        Assert.Equal(welcomeBuffUntilDate, bundle.Character.DoubleExpTime1);
        Assert.Equal(welcomeBuffUntilDate, bundle.Character.DoubleExpTime2);
        Assert.Equal(welcomeBuffUntilDate, bundle.Character.AutoBuffTime);
        Assert.Equal(premiumUntilUnixSeconds, bundle.Character.PremiumExpireUtc);

        Assert.Equal(3, bundle.Items.Count(i => i.Container == 2));
        Assert.Contains(bundle.Items, i => i is { Container: 2, Slot: 7, ItemId: 6 });
        Assert.Contains(bundle.Items, i => i is { Container: 2, Slot: 1, ItemId: 1407, Enchant: 40 });
        Assert.Contains(bundle.Items, i => i is { Container: 0, Slot: 0, ItemId: 1026, Quantity: 999 });

        Assert.Equal(2, bundle.Skills.Count);
        Assert.Contains(bundle.Skills, s => s is { SlotIndex: 0, SkillId: 1, Grade: 1 });

        Assert.Single(bundle.Hotkeys);
        Assert.Contains(bundle.Hotkeys, h => h is { Page: 0, KeyIndex: 0, Sort: 1 });
    }

    [Fact]
    public async Task CreateWithStarterKitAsync_SameSlotTwice_Throws50201_ButADifferentSlotSucceeds()
    {
        var accountId = await CreateTestAccountAsync();

        await CreateMinimalStarterKitCharacterAsync(accountId, 0);

        var ex = await Record.ExceptionAsync(() => CreateMinimalStarterKitCharacterAsync(accountId, 0));
        Assert.NotNull(ex);
        var sqlException = ex as SqlException ?? ex!.InnerException as SqlException;
        if (sqlException is not null)
            Assert.Equal(50201, sqlException.Number);

        var secondCharacterId = await CreateMinimalStarterKitCharacterAsync(accountId, 1);
        Assert.True(secondCharacterId > 0);
    }

    [Fact]
    public async Task CreateWithStarterKitAsync_NameAlreadyTaken_Throws50202()
    {
        var accountA = await CreateTestAccountAsync();
        var accountB = await CreateTestAccountAsync();
        var name = NewCharacterName();

        await _characters.CreateWithStarterKitAsync(
            accountA, 0, name, 0, 0, 0, 0, 1, 0f, 0f, 0f, 100, 100, 50, 50, 0, 0,
            [], [], [], [], CancellationToken.None);

        var ex = await Record.ExceptionAsync(() => _characters.CreateWithStarterKitAsync(
            accountB, 0, name, 0, 0, 0, 0, 1, 0f, 0f, 0f, 100, 100, 50, 50, 0, 0,
            [], [], [], [], CancellationToken.None).AsTask());

        Assert.NotNull(ex);
        var sqlException = ex as SqlException ?? ex!.InnerException as SqlException;
        if (sqlException is not null)
            Assert.Equal(50202, sqlException.Number);
    }

    private Task<int> CreateMinimalStarterKitCharacterAsync(int accountId, byte slot)
    {
        return _characters.CreateWithStarterKitAsync(
            accountId, slot, NewCharacterName(), 0, 0, 0, 0, 1, 0f, 0f, 0f, 100, 100, 50, 50, 0, 0,
            [], [], [], [], CancellationToken.None).AsTask();
    }

    private async Task<int> CreateTestAccountAsync()
    {
        var loginName = $"skittest-{Guid.NewGuid():N}";
        return await _accounts.CreateAsync(loginName, RandomNumberGenerator.GetBytes(32),
            RandomNumberGenerator.GetBytes(16), CancellationToken.None);
    }

    private static string NewCharacterName()
    {
        return $"K{Guid.NewGuid():N}"[..8];
    }
}
