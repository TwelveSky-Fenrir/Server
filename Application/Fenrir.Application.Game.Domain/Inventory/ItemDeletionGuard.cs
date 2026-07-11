using System.Collections.Frozen;

namespace Fenrir.Application.Game.Domain.Inventory;

public static class ItemDeletionGuard
{

        public const int MinValidItemTypeId = 0;

        public const int MaxValidItemTypeId = 99999;

        public static readonly FrozenSet<int> ProtectedItemTypeIds = new[]
    {
        522, 523, 524, 525,
        7101, 7102, 7103, 7104,
        886,
        99011, 99012, 99013, 99014, 99015, 99016
    }.ToFrozenSet();

        public static bool IsDeletionAllowed(int itemTypeId)
    {
        if (itemTypeId is < MinValidItemTypeId or > MaxValidItemTypeId)
            return false;

        return !ProtectedItemTypeIds.Contains(itemTypeId);
    }
}
