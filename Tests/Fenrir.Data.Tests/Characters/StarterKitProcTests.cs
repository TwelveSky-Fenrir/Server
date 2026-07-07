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

    [Fact]
    public async Task CreateWithStarterKitAsync_PersistsStatsPetBuffsPremium_AndEveryCatalogRow()
    {
        var accountId = await CreateTestAccountAsync();
        var name = NewCharacterName();
        var welcomeBuffUntilDate = 20260712; // arbitrary YYYYMMDD, opaque to the proc
        var premiumUntilUnixSeconds = 1_800_000_000L;

        List<CharacterItemSlotTvp> equipment =
        [
            new(2, 8, 1, 0, 0, 0, 0, 0, 0, 0, 0, 0),
            new(7, 6, 1, 0, 0, 0, 0, 0, 0, 0, 0, 0),
            new(1, 1407, 1, 40, 0, 0, 0, 0, 0, 0, 0, 0)
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
            CancellationToken.None, previousTribe: 2);

        Assert.True(characterId > 0);

        var bundle = await _characters.GetWorldEntryBundleAsync(characterId, CancellationToken.None);
        Assert.NotNull(bundle);

        Assert.Equal(2, bundle.Character.PreviousTribe);
        Assert.Equal(1, bundle.Character.StatVit);
        Assert.Equal(1, bundle.Character.StatStr);
        Assert.Equal(1, bundle.Character.StatInt);
        Assert.Equal(1, bundle.Character.StatDex);
        Assert.Equal(640000000, bundle.Character.PetGrowth);
        Assert.Equal((byte)100, bundle.Character.PetActivity);
        // DoubleExpTime1/DoubleExpTime2 are the legacy bare-integer-literal 300 (Server/ts25login/
        // S04_MyWork02.cpp:886-887), NOT the same date as AutoBuffTime -- see
        // Migrations/026_double_exp_time_counter_fix.sql for the full citation and fix rationale.
        Assert.Equal(300, bundle.Character.DoubleExpTime1);
        Assert.Equal(300, bundle.Character.DoubleExpTime2);
        Assert.Equal(welcomeBuffUntilDate, bundle.Character.AutoBuffTime);
        Assert.Equal(premiumUntilUnixSeconds, bundle.Character.PremiumExpireUtc);

        // Server/ts25login/S04_MyWork02.cpp:1100-1120 (USE_CUSTOME_CREATE, force-defined for this whole file --
        // see Migrations/015_starter_kit_elite_grant.sql's header comment): starting level/rebirth/experience/
        // stat-skill-point grant, literal in usp_Character_CreateWithStarterKit regardless of tribe/kit contents.
        Assert.Equal(145, bundle.Character.Level);
        Assert.Equal(12, bundle.Character.Level2);
        Assert.Equal(0, bundle.Character.RebirthCount);
        Assert.Equal(2_000_000_000L, bundle.Character.Experience);
        Assert.Equal(0, bundle.Character.Exp2);
        Assert.Equal(3175, bundle.Character.StatPoints);
        Assert.Equal(10000, bundle.Character.SkillPoints);

        Assert.Equal(3, bundle.Items.Count(i => i.Container == 2));
        Assert.Contains(bundle.Items, i => i is { Container: 2, Slot: 7, ItemId: 6 });
        Assert.Contains(bundle.Items, i => i is { Container: 2, Slot: 1, ItemId: 1407, Enchant: 40 });
        Assert.Contains(bundle.Items, i => i is { Container: 0, Slot: 0, ItemId: 1026, Quantity: 999 });

        Assert.Equal(2, bundle.Skills.Count);
        Assert.Contains(bundle.Skills, s => s is { SlotIndex: 0, SkillId: 1, Grade: 1 });

        Assert.Single(bundle.Hotkeys);
        Assert.Contains(bundle.Hotkeys, h => h is { Page: 0, KeyIndex: 0, Sort: 1 });

        // Starting mount (S04_MyWork02.cpp:1174-1179), now projected by usp_Character_GetForWorldEntry's RS0
        // (Migrations/018_character_previous_tribe_and_mount_readpath.sql) instead of needing a raw SQL
        // read -- this is the exact read path EnterWorldService will eventually consume.
        //
        // These 5 literals are hand-duplicated on the C# side too: Application/Fenrir.Application.Login.Services/
        // CreateAvatar/CreateAvatarService.cs's StarterMountItemId/StarterMountExpActivity/StarterMountPower/
        // StarterMountSlotIndex/StarterMountTime constants overlay the exact same 5 values onto the immediate
        // create-avatar response (CharacterWorldEntryDto's narrow prefix doesn't carry Mount* -- see that DTO's
        // own doc comment -- so CreateAvatarService can't read them back at that point and re-derives them
        // instead). This test only pins the SQL/DB side; Fenrir.Data.Tests has no project reference to
        // Fenrir.Application.Login.Services (by design -- Tests/*Data* stays below the Application layer), so it
        // cannot assert the two sides stay equal in one place. If you change any of these 5 numbers here, go
        // change CreateAvatarService's matching constants too (and vice versa) -- there is no compiler or test
        // that will catch just one side drifting. The clean fix (tracked as a finding this round, not implemented
        // here since it requires editing Application/, outside a database engineer's remit) is to have
        // CreateAvatarService read the freshly-persisted row back via GetWorldEntryBundleAsync/
        // CharacterWorldSnapshotDto -- which already carries all 5 Mount* fields, proven right here -- instead of
        // re-declaring them as a second set of C# literals.
        Assert.Equal(1301, bundle.Character.MountItemId);
        Assert.Equal(0, bundle.Character.MountExpActivity);
        Assert.Equal(5, bundle.Character.MountPower);
        Assert.Equal(0, bundle.Character.MountSlotIndex);
        Assert.Equal(99999999, bundle.Character.MountTime);

        // Starting death-protection allowance (S04_MyWork02.cpp:885-889, unconditional regardless of tribe/
        // previous-tribe/gender), now granted by Migrations/025_character_protect_for_death_grant.sql instead
        // of silently taking the column's DEFAULT of 0.
        Assert.Equal(5, bundle.Character.ProtectForDeath);

        // Starting free auto-hunt minute allowance (S04_MyWork02.cpp:888, same #ifdef LNW33 block as
        // ProtectForDeath/DoubleExpTime1/DoubleExpTime2 above), granted by
        // Migrations/027_character_autotime2_grant.sql. Previously ungrantable (no column existed at all) and,
        // until this assertion, granted-but-untested -- every other literal from this same creation-time block
        // (ProtectForDeath, DoubleExpTime1/2, Mount*, Level/Level2/Experience/Exp2/StatPoints/SkillPoints) was
        // already pinned above/elsewhere in this test, but AutoTime2 itself had no Fenrir.Data.Tests coverage
        // anywhere -- a regression here (e.g. a future corrective migration silently reverting to the column's
        // DEFAULT of 0, or another migration in this same procedure-editing chain rebasing off the wrong
        // ancestor per this migration's own "must be based on the immediately preceding one" warning) would have
        // passed silently.
        Assert.Equal(1440, bundle.Character.AutoTime2);
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
            [], [], [], [], CancellationToken.None, previousTribe: 3).AsTask());

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
