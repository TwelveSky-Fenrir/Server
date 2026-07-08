using Fenrir.Application.Game.Domain.Combat;

namespace Fenrir.Application.Game.Domain.Enchant;

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
///     (Server/Header/function.h:449-461, flat 20% off <see cref="Cost" /> while premium is active) IS applied
///     here via <see cref="GetCost" />, reading <see cref="World.PlayerRuntimeState.PremiumExpireUtc" /> the
///     same "&gt;=, not &gt;" way <see cref="Social.Trade.TradeItemPlacementResolver.IsPremiumPageAccessAllowed" />
///     and <see cref="World.PlayerRuntimeState.RecomputeSupportSkillTimeUpRatio" /> already do -- the caller
///     resolves <c>premiumActive</c> from that field and passes it in, since this resolver stays clock-free.
///     The legacy's server-wide "RANKUP" chat notice (<c>BroadcastNotice</c>, Server/ts25zone/
///     S04_MyWork02.cpp:14746-14750) IS reproduced by the caller (<c>UpgradeCapeService</c>, via
///     <c>IWorldNoticeService</c>) -- unlike <c>CraftPetHandler</c>'s "notable craft" announcement (a
///     DIFFERENT relay mechanism, <c>mCENTER.U_ZONE_BROADCAST_FOR_CENTER_SEND</c>, not this notice's own
///     relay sort 102), <c>BroadcastNotice</c> has a safe, already-shipped Fenrir equivalent -- see
///     <c>IWorldNoticeService</c>'s own remarks for why. This resolver itself stays pure/side-effect-free;
///     the notice is fired by the caller once <see cref="Result.Succeeded" /> is true.
/// </remarks>
public static class CapeUpgradeResolver
{
    public enum Outcome
    {
        Rejected,
        Success,
        Failed
    }

    /// <summary>Base price before the premium discount -- see <see cref="GetCost" />.</summary>
    public const int Cost = 20_000_000;

    private const int PremiumDiscountPercent = 20;

    private const int EmperorCapeItemId = 94100;

    private static readonly HashSet<int> ValidTargetItemIds = [1401, 1403, 1404, 1406, 2208, 2218, 2228, 2238];
    private static readonly HashSet<int> ValidMaterialItemIds = [984, 2394];

    /// <summary>
    ///     <c>Cost</c> reduced by a flat 20% while premium is active (Server/Header/function.h:449-461,
    ///     <c>GetDiscountForPremium</c>), full price otherwise.
    /// </summary>
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
