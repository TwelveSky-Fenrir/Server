using Fenrir.Application.Game.Combat;

namespace Fenrir.Application.Game.Enchant;

/// <summary>
///     Pure resolver for CZ_UP_LEVEL_ITEM_SEND (op127, S04_MyWork02.cpp:14619). Re-skins a cape into a randomly
///     rolled higher tier (money-gated, probability-gated); only the item id changes, quantity/enchant/serial
///     are untouched either way. No I/O, no Zone dependency.
/// </summary>
/// <remarks>
///     <c>aHighItemValue</c> ("lucky upgrade" charge, +5 probability bonus, decremented on use and echoed via
///     ZC sort 29 S029LUCKY_UPGRADE) has no acquisition path anywhere in Fenrir, so <see cref="Resolve" /> is
///     always called with 0 charges -- same posture as <see cref="EnchantResolver" />'s
///     <c>protectForDestroyCharges</c>; that whole bonus branch is currently unreachable. <c>GetDiscountForPremium</c>
///     is not applied: the legacy call site passes <c>mLoginPremium=0</c>, and Fenrir has no <c>aPremium</c>
///     column that could ever be &gt; 0 either, so the discount condition is unreachable regardless --
///     <see cref="Cost" /> is always the full price. The legacy's server-wide "RANKUP" chat notice
///     (<c>BroadcastNotice</c>) has no single-process equivalent and is not reproduced, matching the precedent
///     set for other cross-server notices (e.g. <c>CraftPetHandler</c>'s "notable craft" announcement).
/// </remarks>
public static class CapeUpgradeResolver
{
    public enum Outcome
    {
        Rejected,
        Success,
        Failed
    }

    public const int Cost = 20_000_000;

    private const int EmperorCapeItemId = 94100;

    private static readonly HashSet<int> ValidTargetItemIds = [1401, 1403, 1404, 1406, 2208, 2218, 2228, 2238];
    private static readonly HashSet<int> ValidMaterialItemIds = [984, 2394];

    public static Result Resolve(int targetItemId, int materialItemId, int luck, int highItemValueCharges,
        IRandomSource random)
    {
        if (!ValidTargetItemIds.Contains(targetItemId) || !ValidMaterialItemIds.Contains(materialItemId))
            return new Result(Outcome.Rejected);

        int candidateItemId;
        if (random.NextInt32(3) == 0)
            candidateItemId = random.NextInt32(3) switch { 0 => 1406, 1 => 1403, _ => 1404 };
        else
            candidateItemId = random.NextInt32(4) switch { 0 => 2208, 1 => 2218, 2 => 2228, _ => 2238 };

        if (targetItemId != 1401 && candidateItemId is 1403 or 1404 or 1406 && random.NextInt32(3) == 0)
            candidateItemId = EmperorCapeItemId;

        var probability = 1 + (int)(luck / 500.0f) + (highItemValueCharges > 0 ? 5 : 0);

        return random.NextInt32(1000) < probability
            ? new Result(Outcome.Success, candidateItemId)
            : new Result(Outcome.Failed);
    }

    public readonly record struct Result(Outcome Outcome, int NewItemId = 0)
    {
        public bool Succeeded => Outcome == Outcome.Success;
    }
}
