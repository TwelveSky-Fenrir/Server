using System.Collections.ObjectModel;
using Fenrir.Data.Abstractions.Characters;

namespace Fenrir.Application.Login.Tests.TestSupport;

/// <summary>In-memory stand-in for IStarterKitRepository, seeded with a small but representative Noble Dragon kit.</summary>
internal sealed class FakeStarterKitRepository(StarterKitBundle bundle) : IStarterKitRepository
{
    public (byte PreviousTribe, short MapId)? LastCall { get; private set; }

    public ValueTask<StarterKitBundle> GetByPreviousTribeAsync(byte previousTribe, short mapId, CancellationToken ct)
    {
        LastCall = (previousTribe, mapId);
        return ValueTask.FromResult(bundle);
    }

    /// <summary>
    ///     PreviousTribe 0 (Noble Dragon): G12 Elite Normal Set (Amulet/Armor/Gloves/Ring/Boots) + 3 weapon
    ///     alternatives (raw codes 5/6/7 remapped to elite ids), 4 inventory items, 2 skill slots and 1 hotkey slot
    ///     -- enough for CreateAvatarHandlerTests to assert every field it threads through without reproducing the
    ///     full 30-skill/3-hotkey seed data.
    /// </summary>
    public static FakeStarterKitRepository NobleDragonKit(StarterKitSpawnRowDto? spawn = null)
    {
        var equipment = new ReadOnlyCollection<StarterKitEquipmentRowDto>([
            new StarterKitEquipmentRowDto(0, 84671, null), // Amulet - Wild DemonSoul Necklace
            new StarterKitEquipmentRowDto(2, 84575, null), // Armor - Kahn Guardian Armor
            new StarterKitEquipmentRowDto(3, 84623, null), // Gloves - Glorious Fist Wristband
            new StarterKitEquipmentRowDto(4, 84647, null), // Ring - Twin Head Demon Ring
            new StarterKitEquipmentRowDto(5, 84599, null), // Boots - Island Strider Boots
            new StarterKitEquipmentRowDto(7, 84503, 5), // Sword - Dragon's Fang Sword
            new StarterKitEquipmentRowDto(7, 84527, 6), // Blade - Blade of the Moon
            new StarterKitEquipmentRowDto(7, 84551, 7) // Marble - Great Dragon Eye Marble
        ]);
        var inventory = new ReadOnlyCollection<StarterKitInventoryRowDto>([
            new StarterKitInventoryRowDto(0, 1026, 999),
            new StarterKitInventoryRowDto(1, 1109, 999),
            new StarterKitInventoryRowDto(2, 1224, 999),
            new StarterKitInventoryRowDto(3, 1001, 10)
        ]);
        var skills = new ReadOnlyCollection<StarterKitSkillRowDto>([
            new StarterKitSkillRowDto(0, 1, 1),
            new StarterKitSkillRowDto(1, 2, 1)
        ]);
        var hotkeys = new ReadOnlyCollection<StarterKitHotkeyRowDto>([
            new StarterKitHotkeyRowDto(0, 0, 1, 1, 1)
        ]);

        return new FakeStarterKitRepository(new StarterKitBundle(equipment, inventory, skills, hotkeys,
            spawn ?? new StarterKitSpawnRowDto(6, 0, -7)));
    }

    /// <summary>
    ///     PreviousTribe 1 (Royal Serpent): its own G12 Elite Normal Set, distinct item ids from Noble
    ///     Dragon's (Server/ts25login/S04_MyWork02.cpp:783-809; seed data in
    ///     Database/Migrations/Seed/world/086_starter_kit_equipment_elite_correction.sql) -- 3 weapon
    ///     alternatives (raw codes 11/12/13 remapped to elite ids 85503/85527/85551), same shape as
    ///     <see cref="NobleDragonKit" /> otherwise so CreateAvatarHandlerTests can assert this race's own
    ///     equipment/skill/hotkey ids flow through unmixed with another race's.
    /// </summary>
    public static FakeStarterKitRepository RoyalSerpentKit(StarterKitSpawnRowDto? spawn = null)
    {
        var equipment = new ReadOnlyCollection<StarterKitEquipmentRowDto>([
            new StarterKitEquipmentRowDto(0, 85671, null), // Amulet - Blue Sphere
            new StarterKitEquipmentRowDto(2, 85575, null), // Armor - Everlasting Gold Armor
            new StarterKitEquipmentRowDto(3, 85623, null), // Gloves - Eternal Gold Vambrace
            new StarterKitEquipmentRowDto(4, 85647, null), // Ring - Lunar Cycle Ring
            new StarterKitEquipmentRowDto(5, 85599, null), // Boots - Everlasting Gold Boots
            new StarterKitEquipmentRowDto(7, 85503, 11), // Katana - Thousand Lights
            new StarterKitEquipmentRowDto(7, 85527, 12), // Double Blades - Black Feast
            new StarterKitEquipmentRowDto(7, 85551, 13) // Mandolin - Silversong
        ]);
        var inventory = new ReadOnlyCollection<StarterKitInventoryRowDto>([
            new StarterKitInventoryRowDto(0, 1026, 999),
            new StarterKitInventoryRowDto(1, 1109, 999),
            new StarterKitInventoryRowDto(2, 1224, 999),
            new StarterKitInventoryRowDto(3, 1001, 10)
        ]);
        var skills = new ReadOnlyCollection<StarterKitSkillRowDto>([
            new StarterKitSkillRowDto(0, 20, 1)
        ]);
        var hotkeys = new ReadOnlyCollection<StarterKitHotkeyRowDto>([
            new StarterKitHotkeyRowDto(0, 0, 20, 1, 1)
        ]);

        return new FakeStarterKitRepository(new StarterKitBundle(equipment, inventory, skills, hotkeys,
            spawn ?? new StarterKitSpawnRowDto(-190, 0, 1270)));
    }

    /// <summary>
    ///     PreviousTribe 2 (Grand Tiger): its own G12 Elite Normal Set, distinct item ids from either other
    ///     race (Server/ts25login/S04_MyWork02.cpp:811-838; seed data in
    ///     Database/Migrations/Seed/world/086_starter_kit_equipment_elite_correction.sql) -- 3 weapon
    ///     alternatives (raw codes 17/18/19 remapped to elite ids 86503/86527/86551).
    /// </summary>
    public static FakeStarterKitRepository GrandTigerKit(StarterKitSpawnRowDto? spawn = null)
    {
        var equipment = new ReadOnlyCollection<StarterKitEquipmentRowDto>([
            new StarterKitEquipmentRowDto(0, 86671, null), // Amulet - Haung Long's Heart
            new StarterKitEquipmentRowDto(2, 86575, null), // Armor - Xuan Wu's Tenacity
            new StarterKitEquipmentRowDto(3, 86623, null), // Gloves - 28 Houses Gauntlet
            new StarterKitEquipmentRowDto(4, 86647, null), // Ring - Haung Long's Claw
            new StarterKitEquipmentRowDto(5, 86599, null), // Boots - Four Cardinal Striders
            new StarterKitEquipmentRowDto(7, 86503, 17), // Light Blade - Baihu's Fang
            new StarterKitEquipmentRowDto(7, 86527, 18), // Spear - Qing Long's Grace
            new StarterKitEquipmentRowDto(7, 86551, 19) // Scepter - Zhu Que's Spirit
        ]);
        var inventory = new ReadOnlyCollection<StarterKitInventoryRowDto>([
            new StarterKitInventoryRowDto(0, 1026, 999),
            new StarterKitInventoryRowDto(1, 1109, 999),
            new StarterKitInventoryRowDto(2, 1224, 999),
            new StarterKitInventoryRowDto(3, 1001, 10)
        ]);
        var skills = new ReadOnlyCollection<StarterKitSkillRowDto>([
            new StarterKitSkillRowDto(0, 39, 1)
        ]);
        var hotkeys = new ReadOnlyCollection<StarterKitHotkeyRowDto>([
            new StarterKitHotkeyRowDto(0, 0, 39, 1, 1)
        ]);

        return new FakeStarterKitRepository(new StarterKitBundle(equipment, inventory, skills, hotkeys,
            spawn ?? new StarterKitSpawnRowDto(447, 1, 440)));
    }

    /// <summary>
    ///     Mirrors world.usp_StarterKit_GetByPreviousTribe's real behavior when @PreviousTribe matches no seeded
    ///     race (Server/ts25login/S04_MyWork02.cpp:739-838's switch has no case-3/default branch, so this is a
    ///     reachable, genuine legacy validation gap, not a hypothetical): RS0 (Equipment), RS2 (Skills) and RS3
    ///     (Hotkeys) all filter on PreviousTribe in the proc and come back completely empty, not just missing a
    ///     weapon -- only RS1 (Inventory) is unconditional (no PreviousTribe filter in the proc) and still comes
    ///     back with its usual 4 rows. Using <see cref="NobleDragonKit" /> for this scenario would silently paper
    ///     over that -- it always returns the full ND catalog regardless of which PreviousTribe key is passed in,
    ///     so a test built on it can't tell "only the weapon is missing" apart from "the whole catalog is missing".
    /// </summary>
    public static FakeStarterKitRepository UnseededPreviousTribeKit(StarterKitSpawnRowDto? spawn = null)
    {
        var inventory = new ReadOnlyCollection<StarterKitInventoryRowDto>([
            new StarterKitInventoryRowDto(0, 1026, 999),
            new StarterKitInventoryRowDto(1, 1109, 999),
            new StarterKitInventoryRowDto(2, 1224, 999),
            new StarterKitInventoryRowDto(3, 1001, 10)
        ]);

        return new FakeStarterKitRepository(new StarterKitBundle(
            new ReadOnlyCollection<StarterKitEquipmentRowDto>([]),
            inventory,
            new ReadOnlyCollection<StarterKitSkillRowDto>([]),
            new ReadOnlyCollection<StarterKitHotkeyRowDto>([]),
            spawn ?? new StarterKitSpawnRowDto(6, 0, -7)));
    }
}
