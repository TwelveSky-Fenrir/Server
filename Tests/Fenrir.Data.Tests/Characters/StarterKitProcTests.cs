using System.Security.Cryptography;
using CaeriusNet.Abstractions;
using CaeriusNet.Builders;
using Fenrir.Data.Abstractions.Accounts;
using Fenrir.Data.Abstractions.Characters;
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
    }

    [Fact]
    public async Task GetByPreviousTribeAsync_NobleDragon_ReturnsTheSeededCatalogAndZone1sSpawn()
    {
        var bundle = await _starterKits.GetByPreviousTribeAsync(0, 1, CancellationToken.None);

        // G12 Elite Normal Set: Amulet+Armor+Gloves+Ring+Boots + 3 weapon alternatives (raw code -> elite id).
        Assert.Equal(8, bundle.Equipment.Count);
        Assert.Contains(bundle.Equipment, e => e is { EquipSlot: 0, ItemId: 84671, RawWeaponCode: null });
        Assert.Contains(bundle.Equipment, e => e is { EquipSlot: 2, ItemId: 84575, RawWeaponCode: null });
        Assert.Contains(bundle.Equipment, e => e is { EquipSlot: 3, ItemId: 84623, RawWeaponCode: null });
        Assert.Contains(bundle.Equipment, e => e is { EquipSlot: 4, ItemId: 84647, RawWeaponCode: null });
        Assert.Contains(bundle.Equipment, e => e is { EquipSlot: 5, ItemId: 84599, RawWeaponCode: null });
        Assert.Contains(bundle.Equipment, e => e is { EquipSlot: 7, ItemId: 84503, RawWeaponCode: 5 });
        Assert.Contains(bundle.Equipment, e => e is { EquipSlot: 7, ItemId: 84527, RawWeaponCode: 6 });
        Assert.Contains(bundle.Equipment, e => e is { EquipSlot: 7, ItemId: 84551, RawWeaponCode: 7 });

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

        // G12 Elite Normal Set (S04_MyWork02.cpp:783-809): Amulet+Armor+Gloves+Ring+Boots + 3 weapon
        // alternatives (raw code -> elite id). Asserted row-for-row, like the Noble Dragon test above, so a
        // seed-data mixup between races (e.g. an ND item id leaking into RS's catalog) fails here rather than
        // only being caught by the generic-across-races C# service logic.
        Assert.Equal(8, bundle.Equipment.Count);
        Assert.Contains(bundle.Equipment, e => e is { EquipSlot: 0, ItemId: 85671, RawWeaponCode: null });
        Assert.Contains(bundle.Equipment, e => e is { EquipSlot: 2, ItemId: 85575, RawWeaponCode: null });
        Assert.Contains(bundle.Equipment, e => e is { EquipSlot: 3, ItemId: 85623, RawWeaponCode: null });
        Assert.Contains(bundle.Equipment, e => e is { EquipSlot: 4, ItemId: 85647, RawWeaponCode: null });
        Assert.Contains(bundle.Equipment, e => e is { EquipSlot: 5, ItemId: 85599, RawWeaponCode: null });
        Assert.Contains(bundle.Equipment, e => e is { EquipSlot: 7, ItemId: 85503, RawWeaponCode: 11 });
        Assert.Contains(bundle.Equipment, e => e is { EquipSlot: 7, ItemId: 85527, RawWeaponCode: 12 });
        Assert.Contains(bundle.Equipment, e => e is { EquipSlot: 7, ItemId: 85551, RawWeaponCode: 13 });

        Assert.Contains(bundle.Skills, s => s is { SlotIndex: 0, SkillId: 20, Grade: 1 });
        Assert.Contains(bundle.Hotkeys, h => h is { Page: 0, KeyIndex: 0, Sort: 20, Value1: 1, Value2: 1 });

        Assert.NotNull(bundle.Spawn);
        Assert.Equal(-190f, bundle.Spawn!.PosX);
        Assert.Equal(0f, bundle.Spawn.PosY);
        Assert.Equal(1270f, bundle.Spawn.PosZ);
    }

    [Fact]
    public async Task GetByPreviousTribeAsync_GrandTiger_ReturnsItsOwnCatalogAndZone11sSpawn()
    {
        var bundle = await _starterKits.GetByPreviousTribeAsync(2, 11, CancellationToken.None);

        // G12 Elite Normal Set (S04_MyWork02.cpp:811-838): Amulet+Armor+Gloves+Ring+Boots + 3 weapon
        // alternatives (raw code -> elite id). Grand Tiger had zero test coverage anywhere in this suite prior
        // to this test -- only Noble Dragon and (partially) Royal Serpent were previously asserted.
        Assert.Equal(8, bundle.Equipment.Count);
        Assert.Contains(bundle.Equipment, e => e is { EquipSlot: 0, ItemId: 86671, RawWeaponCode: null });
        Assert.Contains(bundle.Equipment, e => e is { EquipSlot: 2, ItemId: 86575, RawWeaponCode: null });
        Assert.Contains(bundle.Equipment, e => e is { EquipSlot: 3, ItemId: 86623, RawWeaponCode: null });
        Assert.Contains(bundle.Equipment, e => e is { EquipSlot: 4, ItemId: 86647, RawWeaponCode: null });
        Assert.Contains(bundle.Equipment, e => e is { EquipSlot: 5, ItemId: 86599, RawWeaponCode: null });
        Assert.Contains(bundle.Equipment, e => e is { EquipSlot: 7, ItemId: 86503, RawWeaponCode: 17 });
        Assert.Contains(bundle.Equipment, e => e is { EquipSlot: 7, ItemId: 86527, RawWeaponCode: 18 });
        Assert.Contains(bundle.Equipment, e => e is { EquipSlot: 7, ItemId: 86551, RawWeaponCode: 19 });

        Assert.Contains(bundle.Skills, s => s is { SlotIndex: 0, SkillId: 39, Grade: 1 });
        Assert.Contains(bundle.Hotkeys, h => h is { Page: 0, KeyIndex: 0, Sort: 39, Value1: 1, Value2: 1 });

        Assert.NotNull(bundle.Spawn);
        Assert.Equal(447f, bundle.Spawn!.PosX);
        Assert.Equal(1f, bundle.Spawn.PosY);
        Assert.Equal(440f, bundle.Spawn.PosZ);
    }

    [Fact]
    public async Task GetByPreviousTribeAsync_UnseededMapId_LeavesSpawnNull()
    {
        var bundle = await _starterKits.GetByPreviousTribeAsync(0, short.MaxValue, CancellationToken.None);

        Assert.Null(bundle.Spawn);
    }

    // character-creation-level1-redesign (CONFIRMED PRODUCT DECISION, see
    // Database/Migrations/027_character_create_level1_basic_kit.sql's own header -- NOT a legacy-parity
    // assertion): a fresh character now persists at Level 1 with a weapon+torso-armor-only equipment set, no
    // mount/pet-growth/premium/death-protection/auto-hunt/double-exp grant. Renamed from the old
    // ..._PersistsStatsPetBuffsPremium_... name, which asserted exactly the EU33 instant-elite grant this
    // redesign removes.
    [Fact]
    public async Task CreateWithStarterKitAsync_PersistsLevel1StatsAndBasicWeaponArmorKit()
    {
        var accountId = await CreateTestAccountAsync();
        var name = NewCharacterName();
        var welcomeBuffUntilDate = 20260712; // arbitrary YYYYMMDD, opaque to the proc
        var premiumUntilUnixSeconds = 1_800_000_000L; // no longer written anywhere -- see assertion below

        List<CharacterItemSlotTvp> equipment =
        [
            new(2, 8, 1, 0, 0, 0, 0, 0, 0, 0, 0, 0),
            new(7, 6, 1, 0, 0, 0, 0, 0, 0, 0, 0, 0)
        ];
        List<CharacterItemSlotTvp> inventory = [new(0, 1026, 999, 0, 0, 0, 0, 0, 0, 0, 0, 0)];
        List<CharacterSkillSlotTvp> skills = [new(0, 1, 1), new(1, 2, 1)];
        List<CharacterHotkeySlotTvp> hotkeys = [new(0, 0, 1, 1, 1)];

        // previousTribe (2, Grand Tiger) is deliberately different from tribe (0) below -- proves the two
        // persist as genuinely independent columns (Server/ts25zone/S04_MyWork02.cpp:880-901's self-
        // consistency check treats them as two distinct fields, never one derived from the other) rather
        // than PreviousTribe silently mirroring Tribe.
        var characterId = await _characters.CreateWithStarterKitAsync(
            accountId, 0, name, 0, 1, 2, 1,
            1, 6f, 0f, -7f,
            100, 100, 50, 50,
            welcomeBuffUntilDate, premiumUntilUnixSeconds,
            equipment, inventory, skills, hotkeys,
            CancellationToken.None, 2);

        Assert.True(characterId > 0);

        var bundle = await _characters.GetWorldEntryBundleAsync(characterId, CancellationToken.None);
        Assert.NotNull(bundle);

        Assert.Equal(2, bundle.Character.PreviousTribe);
        Assert.Equal(1, bundle.Character.StatVit);
        Assert.Equal(1, bundle.Character.StatStr);
        Assert.Equal(1, bundle.Character.StatInt);
        Assert.Equal(1, bundle.Character.StatDex);
        // No pet is ever granted under this redesign -- PetGrowth/PetActivity take game.Characters' own
        // column DEFAULTs (0/0) instead of the old 640,000,000/100 literal.
        Assert.Equal(0, bundle.Character.PetGrowth);
        Assert.Equal((byte)0, bundle.Character.PetActivity);
        // DoubleExpTime1/DoubleExpTime2 were the legacy bare-integer-literal 300 (Server/ts25login/
        // S04_MyWork02.cpp:886-887) under the old EU33 design -- this redesign drops that "instant boost"
        // grant entirely, so both take game.Characters' own column DEFAULT of 0.
        Assert.Equal(0, bundle.Character.DoubleExpTime1);
        Assert.Equal(0, bundle.Character.DoubleExpTime2);
        // AutoBuffTime (the welcome-buff/second-inventory-page/second-store-page rental grant) is unaffected
        // by the redesign and still the caller-supplied date.
        Assert.Equal(welcomeBuffUntilDate, bundle.Character.AutoBuffTime);
        // No premium-day grant anymore -- PremiumExpireUtc takes the column's own DEFAULT of 0 regardless of
        // the (now-ignored) @PremiumUntilUnixSeconds argument passed above. See
        // Database/Migrations/027_character_create_level1_basic_kit.sql's own header for why the parameter
        // is kept declared but unused.
        Assert.Equal(0L, bundle.Character.PremiumExpireUtc);

        // CONFIRMED PRODUCT DECISION (character-creation-level1-redesign, NOT a legacy citation): a genuine
        // Level 1 character, zero experience, zero rebirths, no post-cap "high level" ladder progress.
        // StatPoints/SkillPoints are Fenrir product defaults -- see the migration's own header for the full
        // "why 50/0" reasoning (also mirrored on the C# side by CreateAvatarService.StartingStatPoint/
        // StartingSkillPoint).
        Assert.Equal(1, bundle.Character.Level);
        Assert.Equal(0, bundle.Character.Level2);
        Assert.Equal(0, bundle.Character.RebirthCount);
        Assert.Equal(0L, bundle.Character.Experience);
        Assert.Equal(0, bundle.Character.Exp2);
        Assert.Equal(50, bundle.Character.StatPoints);
        Assert.Equal(0, bundle.Character.SkillPoints);

        // Exactly the chosen Weapon + the tribe's Armor/torso row -- no Amulet/Gloves/Ring/Boots, no Cape, no
        // Pet.
        Assert.Equal(2, bundle.Items.Count(i => i.Container == 2));
        Assert.Contains(bundle.Items, i => i is { Container: 2, Slot: 7, ItemId: 6 });
        Assert.Contains(bundle.Items, i => i is { Container: 2, Slot: 2, ItemId: 8 });
        Assert.DoesNotContain(bundle.Items, i => i is { Container: 2, Slot: 1 }); // Cape
        Assert.DoesNotContain(bundle.Items, i => i is { Container: 2, Slot: 8 }); // Pet
        Assert.Contains(bundle.Items, i => i is { Container: 0, Slot: 0, ItemId: 1026, Quantity: 999 });

        Assert.Equal(2, bundle.Skills.Count);
        Assert.Contains(bundle.Skills, s => s is { SlotIndex: 0, SkillId: 1, Grade: 1 });

        Assert.Single(bundle.Hotkeys);
        Assert.Contains(bundle.Hotkeys, h => h is { Page: 0, KeyIndex: 0, Sort: 1 });

        // No starter mount anymore -- Mount* all take game.Characters' own column DEFAULTs (MountSlotIndex's
        // is specifically -1, "no mount active"), not the old universal-tiger-mount literals.
        Assert.Equal(0, bundle.Character.MountItemId);
        Assert.Equal(0, bundle.Character.MountExpActivity);
        Assert.Equal(0, bundle.Character.MountPower);
        Assert.Equal(-1, bundle.Character.MountSlotIndex);
        Assert.Equal(0, bundle.Character.MountTime);

        // No starting death-protection allowance or free auto-hunt minute allowance anymore -- both take
        // game.Characters' own column DEFAULT of 0, not the old LNW33 "instant boost" literals (5/1440).
        Assert.Equal(0, bundle.Character.ProtectForDeath);
        Assert.Equal(0, bundle.Character.AutoTime2);
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

    // db-createwithstarterkit-fieldaudit (Major): closes this suite's own blind spot -- prior to this test, no
    // test in Fenrir.Data.Tests or Fenrir.IntegrationTests ever exercised an out-of-range previousTribe against
    // the real stored procedure (the only real-database test covering this proc used previousTribe: 2, in
    // range). Confirms CK_Characters_PreviousTribe (Migrations/018_character_previous_tribe_and_mount_readpath.
    // sql:35-36) actually fires as a raw, uncoded SQL Server CHECK-constraint violation (error 547) -- not one
    // of this procedure's own purpose-coded THROW checks (50201/50202) -- on the transaction's first INSERT,
    // confirming CreateAvatarHandler's own PreviousTribe range check (0-2) is what now stands between a
    // malformed/tampered request and this raw database failure, rather than the handler's structural
    // validation never mattering in practice.
    [Fact]
    public async Task CreateWithStarterKitAsync_PreviousTribeOutOfRange_ThrowsCheckConstraintViolation()
    {
        var accountId = await CreateTestAccountAsync();
        var name = NewCharacterName();

        var ex = await Record.ExceptionAsync(() => _characters.CreateWithStarterKitAsync(
            accountId, 0, name, 0, 0, 0, 0, 1, 0f, 0f, 0f, 100, 100, 50, 50, 0, 0,
            [], [], [], [], CancellationToken.None, 3).AsTask());

        Assert.NotNull(ex);
        var sqlException = ex as SqlException ?? ex!.InnerException as SqlException;
        Assert.NotNull(sqlException);
        Assert.Equal(547, sqlException!.Number);
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
