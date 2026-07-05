using System.Collections.ObjectModel;
using Fenrir.Data.Characters;

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

    /// <summary>PreviousTribe 0 (Noble Dragon): 3 weapon alternatives (5/6/7), armor/gloves/boots, 4 inventory
    /// items, 2 skill slots and 1 hotkey slot -- enough for CreateAvatarHandlerTests to assert every field it
    /// threads through without reproducing the full 30-skill/3-hotkey seed data.</summary>
    public static FakeStarterKitRepository NobleDragonKit(StarterKitSpawnRowDto? spawn = null)
    {
        var equipment = new ReadOnlyCollection<StarterKitEquipmentRowDto>([
            new StarterKitEquipmentRowDto(2, 8),
            new StarterKitEquipmentRowDto(3, 9),
            new StarterKitEquipmentRowDto(5, 10),
            new StarterKitEquipmentRowDto(7, 5),
            new StarterKitEquipmentRowDto(7, 6),
            new StarterKitEquipmentRowDto(7, 7)
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
}
