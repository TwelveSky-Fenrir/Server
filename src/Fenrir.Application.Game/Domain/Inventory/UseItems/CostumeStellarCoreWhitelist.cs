using System.Collections.Frozen;
using Fenrir.Domain.Game.Stats;

namespace Fenrir.Application.Game.Domain.Inventory.UseItems;

public static class CostumeStellarCoreWhitelist
{
    private const int Tier1Start = 76527;

    private const int Tier1End = 76540;
    private const int Tier2Start = 93500;
    private const int Tier2End = 93513;

    public static readonly FrozenSet<int> ValidStellarCoreIds = BuildValidStellarCoreIds();

    private static FrozenSet<int> BuildValidStellarCoreIds()
    {
        var ids = new HashSet<int>();
        for (var id = Tier1Start; id <= Tier1End; id++)
            ids.Add(id);
        for (var id = Tier2Start; id <= Tier2End; id++)
            ids.Add(id);
        return ids.ToFrozenSet();
    }

    public static bool IsValidCostume(int itemId)
    {
        return StatCalculator.ValidCostumeIds.Contains(itemId);
    }

    public static bool IsValidStellarCore(int itemId)
    {
        return ValidStellarCoreIds.Contains(itemId);
    }

    public static bool ClaimsItem(int itemId)
    {
        return IsValidCostume(itemId) || IsValidStellarCore(itemId);
    }
}
