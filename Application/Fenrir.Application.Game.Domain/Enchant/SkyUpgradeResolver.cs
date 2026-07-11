using Fenrir.Application.Game.Domain.Combat;

namespace Fenrir.Application.Game.Domain.Enchant;

public static class SkyUpgradeResolver
{
    public enum Outcome
    {
        Rejected,
        Success,
        Failed
    }

    public const int Cost = 50_000_000;
    public const int MinTargetItemId = 87000;
    public const int MaxTargetItemId = 87128;
    public const int MinRequiredEnchant = 30;
    public const int ItemIdShift = 129;

    private const byte MinEquipSort = 7;
    private const byte MaxEquipSort = 29;
    private const byte RequiredCheckSetItem = 2;

    private static readonly Dictionary<int, MaterialTier> MaterialTiers = new()
    {
        [501] = new MaterialTier(40, -8),
        [502] = new MaterialTier(30, -6),
        [503] = new MaterialTier(20, -4),
        [504] = new MaterialTier(10, 0),
        [505] = new MaterialTier(10, 0)
    };

    public static Result Resolve(ItemRowDto targetItem, byte targetEnchant, int materialItemId,
        IRandomSource random)
    {
        if (targetItem.Sort is < MinEquipSort or > MaxEquipSort || targetItem.CheckSetItem != RequiredCheckSetItem ||
            targetItem.ItemId is < MinTargetItemId or > MaxTargetItemId || targetEnchant < MinRequiredEnchant ||
            !MaterialTiers.TryGetValue(materialItemId, out var tier))
            return new Result(Outcome.Rejected);

        if (random.NextInt32(100) >= tier.ProbabilityPercent)
            return new Result(Outcome.Failed);

        var newEnchant = tier.EnchantDecrease < 0
            ? (byte)(targetEnchant + tier.EnchantDecrease)
            : targetEnchant;

        return new Result(Outcome.Success, targetItem.ItemId + ItemIdShift, newEnchant);
    }

    private readonly record struct MaterialTier(int ProbabilityPercent, int EnchantDecrease);

    public readonly record struct Result(Outcome Outcome, int NewItemId = 0, byte NewEnchant = 0)
    {
        public bool Succeeded => Outcome == Outcome.Success;
    }
}
