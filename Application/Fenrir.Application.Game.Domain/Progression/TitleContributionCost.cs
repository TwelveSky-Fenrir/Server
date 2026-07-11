namespace Fenrir.Application.Game.Domain.Progression;

/// <summary>
///     Single source of truth for the tribe "title" (kill-other-tribe contribution-point) economy: the per-rank
///     step cost the purchase path debits (tribe-work sort 6) and the cumulative refund the title-remove scroll
///     grants (items 1200/8419/1494). The refund is the piece the C19 finding flagged as missing/wrong: it is a
///     <b>cumulative</b> sum of every rank below the current title, not a single table entry.
/// </summary>
/// <remarks>
///     Réf. C++ : Server/ts25zone/S07_MyGame03.cpp:25-37 and H07_MyGame.h:526 (the 13-entry <c>mTitleCostCP</c>
///     table). Server/ts25zone/S07_MyGame03.cpp:9141-9163 (<c>ReturnKillOtherTribeFromTitle</c>: title portion =
///     stored title modulo 100; guard rejects portion &lt; 1, portion &gt; max, type &lt; 0 or type &gt; 1 by
///     returning 0; cumulative cost = sum of the table for every rank BELOW the current title; type 1 keeps 70%).
///     Server/ts25zone/S07_MyGame03.cpp:9144-9148 + the repo-wide absence of any <c>#define __TITLE__LV13__</c>
///     (max title portion is 12 in the shipped ReleaseEU33 build, so table index 12 is never summed and only
///     reachable under the dead LV13 config). Server/ts25zone/S04_MyWork03.cpp:4654-4681 + use_inventory.h:40
///     (<c>WUSE_ITEM_1200</c> refund consumer: 1200/8419 -&gt; full refund, 1494 -&gt; 70%).
///     Server/ts25zone/S04_MyWork02.cpp:11094-11127 (tribe-work sort 6 purchase: portion guard 0-11, single-step
///     cost = table entry at the current portion).
/// </remarks>
public static class TitleContributionCost
{
    /// <summary>Highest title portion the shipped build allows (LV13 config is not compiled).</summary>
    public const int MaxTitlePortion = 12;

    /// <summary>Refund type: full cumulative cost returned.</summary>
    public const int RefundTypeFull = 0;

    /// <summary>Refund type: 70% of the cumulative cost returned (30% reduction).</summary>
    public const int RefundTypeReduced = 1;

    /// <summary>
    ///     Per-rank cost table. Index = the current title portion before a purchase (0-11 for a live purchase,
    ///     12 only reachable under the dead LV13 config). The 13th entry is kept for table fidelity.
    /// </summary>
    public static readonly int[] CostTable =
        [800, 1700, 2500, 3400, 4200, 5100, 5900, 6800, 7600, 8500, 9300, 10000, 10000];

    /// <summary>The count of title ranks currently held: the stored title value taken modulo 100.</summary>
    public static int PortionOf(int storedTitle)
    {
        return storedTitle % 100;
    }

    /// <summary>
    ///     Single-step purchase cost for advancing from <paramref name="currentPortion" /> to the next rank
    ///     (tribe-work sort 6). Returns 0 for a portion outside 0-11, matching the purchase handler's own guard
    ///     (the caller disconnects on that out-of-range case; this method never itself grants free progress).
    /// </summary>
    public static int PurchaseStepCost(int currentPortion)
    {
        if (currentPortion is < 0 or > 11)
            return 0;

        return CostTable[currentPortion];
    }

    /// <summary>
    ///     Maps a title-remove scroll item id to its refund type. Items 1200 and 8419 give a full refund
    ///     (<see cref="RefundTypeFull" />); item 1494 gives the reduced 70% refund
    ///     (<see cref="RefundTypeReduced" />). Any other id returns false: not a title-remove scroll.
    /// </summary>
    public static bool TryResolveRefundType(int scrollItemId, out int refundType)
    {
        switch (scrollItemId)
        {
            case 1200 or 8419:
                refundType = RefundTypeFull;
                return true;
            case 1494:
                refundType = RefundTypeReduced;
                return true;
            default:
                refundType = -1;
                return false;
        }
    }

    /// <summary>
    ///     The contribution-point refund for removing a character's title -- the cumulative sum of the cost of
    ///     every rank BELOW the current title portion, optionally reduced to 70%. Returns 0 (a silent no-op the
    ///     legacy applies without an error) when the title portion is outside 1-<see cref="MaxTitlePortion" /> or
    ///     the refund type is outside 0-1.
    /// </summary>
    /// <param name="storedTitle">The character's stored title value; its portion is taken modulo 100.</param>
    /// <param name="refundType"><see cref="RefundTypeFull" /> (0) or <see cref="RefundTypeReduced" /> (1).</param>
    public static int CumulativeRefund(int storedTitle, int refundType)
    {
        if (refundType is < RefundTypeFull or > RefundTypeReduced)
            return 0;

        var portion = PortionOf(storedTitle);
        if (portion is < 1 or > MaxTitlePortion)
            return 0;

        // Sum every rank strictly below the current portion (the legacy loop stops one short of it), so the
        // final table entry at index 12 is never included -- reachable only under the dead LV13 config.
        var total = 0;
        for (var rank = 0; rank < portion; rank++)
            total += CostTable[rank];

        // 70% for the reduced type; integer arithmetic (matches the legacy's truncating double cast, since both
        // paths floor). Full-refund type returns the sum unchanged.
        return refundType == RefundTypeReduced ? total * 70 / 100 : total;
    }
}
