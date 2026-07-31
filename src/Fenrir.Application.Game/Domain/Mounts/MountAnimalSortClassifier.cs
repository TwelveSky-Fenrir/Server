using System.Collections.Frozen;
using Fenrir.Domain.Game.GameData;

namespace Fenrir.Application.Game.Domain.Mounts;

public static class MountAnimalSortClassifier
{
    public const int NoItem = 0;

    public const int GenericMount = 3;

    public const int NewMountItemSort = 30;

    public static int Classify(int animalItemId, FrozenDictionary<int, ItemDefinition> itemsById)
    {
        if (!itemsById.TryGetValue(animalItemId, out var item))
            return NoItem;

        return item.Item.Sort == NewMountItemSort ? NewMountItemSort : GenericMount;
    }
}
