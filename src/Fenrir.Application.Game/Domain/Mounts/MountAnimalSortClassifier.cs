using System.Collections.Frozen;
using Fenrir.Domain.Game.GameData;

namespace Fenrir.Application.Game.Domain.Mounts;

public static class MountAnimalSortClassifier
{
    public const int NoItem = 0;

    public const int GenericMount = 3;

    public const int NewMountItemSort = 30;

    public static bool TryResolveAuthoritativeItemSort(int animalItemId,
        FrozenDictionary<int, ItemDefinition> itemsById, out int itemSort)
    {
        if (itemsById.TryGetValue(animalItemId, out var item))
        {
            itemSort = item.Item.Sort;
            return true;
        }

        itemSort = default;
        return false;
    }

    public static int Classify(int animalItemId, FrozenDictionary<int, ItemDefinition> itemsById)
    {
        if (!TryResolveAuthoritativeItemSort(animalItemId, itemsById, out var itemSort))
            return NoItem;

        return itemSort == NewMountItemSort ? NewMountItemSort : GenericMount;
    }
}
