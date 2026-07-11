using System.Collections.ObjectModel;
using Fenrir.Data.Abstractions.Characters;

namespace Fenrir.Application.Login.Tests.TestSupport;

internal sealed class FakeStarterKitRepository(StarterKitBundle bundle) : IStarterKitRepository
{
    public (byte PreviousTribe, short MapId)? LastCall { get; private set; }

    public ValueTask<StarterKitBundle> GetByPreviousTribeAsync(byte previousTribe, short mapId, CancellationToken ct)
    {
        LastCall = (previousTribe, mapId);
        return ValueTask.FromResult(bundle);
    }

    public static FakeStarterKitRepository NobleDragonKit(StarterKitSpawnRowDto? spawn = null)
    {
        var equipment = new ReadOnlyCollection<StarterKitEquipmentRowDto>([
            new StarterKitEquipmentRowDto(0, 84671, null),
            new StarterKitEquipmentRowDto(2, 84575, null),
            new StarterKitEquipmentRowDto(3, 84623, null),
            new StarterKitEquipmentRowDto(4, 84647, null),
            new StarterKitEquipmentRowDto(5, 84599, null),
            new StarterKitEquipmentRowDto(7, 84503, 5),
            new StarterKitEquipmentRowDto(7, 84527, 6),
            new StarterKitEquipmentRowDto(7, 84551, 7)
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

    public static FakeStarterKitRepository RoyalSerpentKit(StarterKitSpawnRowDto? spawn = null)
    {
        var equipment = new ReadOnlyCollection<StarterKitEquipmentRowDto>([
            new StarterKitEquipmentRowDto(0, 85671, null),
            new StarterKitEquipmentRowDto(2, 85575, null),
            new StarterKitEquipmentRowDto(3, 85623, null),
            new StarterKitEquipmentRowDto(4, 85647, null),
            new StarterKitEquipmentRowDto(5, 85599, null),
            new StarterKitEquipmentRowDto(7, 85503, 11),
            new StarterKitEquipmentRowDto(7, 85527, 12),
            new StarterKitEquipmentRowDto(7, 85551, 13)
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

    public static FakeStarterKitRepository GrandTigerKit(StarterKitSpawnRowDto? spawn = null)
    {
        var equipment = new ReadOnlyCollection<StarterKitEquipmentRowDto>([
            new StarterKitEquipmentRowDto(0, 86671, null),
            new StarterKitEquipmentRowDto(2, 86575, null),
            new StarterKitEquipmentRowDto(3, 86623, null),
            new StarterKitEquipmentRowDto(4, 86647, null),
            new StarterKitEquipmentRowDto(5, 86599, null),
            new StarterKitEquipmentRowDto(7, 86503, 17),
            new StarterKitEquipmentRowDto(7, 86527, 18),
            new StarterKitEquipmentRowDto(7, 86551, 19)
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
