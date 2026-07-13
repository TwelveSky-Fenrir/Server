using Fenrir.Application.Game.Domain.Inventory;

namespace Fenrir.Application.Game.Domain.Forge;

public static class DestroyResolver
{
    public enum DestroyOutcome
    {
        Rejected,
        Success
    }

    public const byte RareItemType = 3;

    public static DestroyResult Resolve(ItemRowDto targetItem, ItemStack targetStack)
    {
        var isValue = targetStack.Enchant;

        if (isValue < 1 || targetItem.Type != RareItemType || targetItem.Sort is < 7 or > 29 ||
            targetItem.CheckImprove != 2)
            return DestroyResult.Rejected;

        var (money, stoneItemId) = isValue switch
        {
            >= 1 and <= 10 => (1_000_000, 1021),
            >= 11 and <= 20 => (2_000_000, 1021),
            >= 21 and <= 30 => (3_000_000, 1022),
            >= 31 and <= 39 => (5_000_000, 1023),
            _ => (10_000_000, 1437)
        };

        return new DestroyResult(DestroyOutcome.Success, money, stoneItemId);
    }

    public readonly record struct DestroyResult(DestroyOutcome Outcome, int Money, int StoneItemId)
    {
        public static readonly DestroyResult Rejected = new(DestroyOutcome.Rejected, 0, 0);
    }
}
