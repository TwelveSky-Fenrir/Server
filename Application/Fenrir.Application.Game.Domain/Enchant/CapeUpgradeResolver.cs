using Fenrir.Application.Game.Domain.Combat;

namespace Fenrir.Application.Game.Domain.Enchant;

public static class CapeUpgradeResolver
{
    public enum Outcome
    {
        Rejected,
        Success,
        Failed
    }

    public const int Cost = 20_000_000;

    private const int PremiumDiscountPercent = 20;

    private const int EmperorCapeItemId = 94100;

    private static readonly HashSet<int> ValidTargetItemIds = [1401, 1403, 1404, 1406, 2208, 2218, 2228, 2238];
    private static readonly HashSet<int> ValidMaterialItemIds = [984, 2394];

    public static int GetCost(bool premiumActive)
    {
        return premiumActive ? Cost - Cost * PremiumDiscountPercent / 100 : Cost;
    }

    public static Result Resolve(int targetItemId, int materialItemId, int luck, int highItemValueCharges,
        bool premiumActive, IRandomSource random)
    {
        if (!ValidTargetItemIds.Contains(targetItemId) || !ValidMaterialItemIds.Contains(materialItemId))
            return new Result(Outcome.Rejected);

        var cost = GetCost(premiumActive);

        int candidateItemId;
        if (random.NextInt32(3) == 0)
            candidateItemId = random.NextInt32(3) switch { 0 => 1406, 1 => 1403, _ => 1404 };
        else
            candidateItemId = random.NextInt32(4) switch { 0 => 2208, 1 => 2218, 2 => 2228, _ => 2238 };

        if (targetItemId != 1401 && candidateItemId is 1403 or 1404 or 1406 && random.NextInt32(3) == 0)
            candidateItemId = EmperorCapeItemId;

        var probability = 1 + (int)(luck / 500.0f) + (highItemValueCharges > 0 ? 5 : 0);

        return random.NextInt32(1000) < probability
            ? new Result(Outcome.Success, cost, candidateItemId)
            : new Result(Outcome.Failed, cost);
    }

    public readonly record struct Result(Outcome Outcome, int Cost = 0, int NewItemId = 0)
    {
        public bool Succeeded => Outcome == Outcome.Success;
    }
}
