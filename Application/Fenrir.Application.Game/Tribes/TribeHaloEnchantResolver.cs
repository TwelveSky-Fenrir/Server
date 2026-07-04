using Fenrir.Application.Game.Combat;

namespace Fenrir.Application.Game.Tribes;

/// <summary>Outcome of one TRIBE_WORK tSort 7 halo-enchant attempt (doc 10 §2, ZC_TRIBE_WORK_RECV tResult 0/1/2).</summary>
public enum TribeHaloEnchantOutcome
{
    /// <summary>tResult 0 -- <c>aHalo += 1</c> (GL_870_HALO_HEAD issue 1).</summary>
    Success,

    /// <summary>tResult 1 -- a consumable <c>aProtectForHalo</c> charge absorbed the downgrade (issue 2).</summary>
    ProtectionConsumed,

    /// <summary>tResult 2 -- <c>aHalo -= 1</c> (issue 3).</summary>
    Downgraded,

    /// <summary>tResult 1 -- neither success nor downgrade (issue 4; also the ONLY outcome possible at aHalo==0, since the source has no explicit "downgrade below 0" branch).</summary>
    NeutralFail
}

/// <summary>
///     Pure port of <c>GetHaloCostumeEnchantRate</c> (verified in full, <c>Server/Header/function.h:2165-2214</c>)
///     plus the two-roll decision tree around it (<c>Server/ts25zone/S04_MyWork02.cpp:11128-11230</c>, TRIBE_WORK
///     tSort 7). No I/O or state dependency -- <see cref="Tribes.TribeActionHandler" /> supplies the current
///     aHalo/aProtectForHalo and reads the result back.
/// </summary>
public static class TribeHaloEnchantResolver
{
    /// <summary>
    ///     <c>GetHaloCostumeEnchantRate(tCurrentImprove, &amp;tSuccess, &amp;tFail, &amp;tDecrease)</c> --
    ///     <paramref name="currentHalo" /> is <c>aHalo</c> (the C++ adds 1 internally, "pCurrentImprove").
    ///     <see cref="SuccessRate" /> is a flat 15 regardless of tier; only <see cref="DecreaseRate" /> varies,
    ///     +3 per 10-point bracket of <c>pCurrentImprove</c>, up to 30 at 90+. Both are percentage points for
    ///     a <c>rand() % 100</c> roll.
    /// </summary>
    public static (int SuccessRate, int DecreaseRate) GetRates(int currentHalo)
    {
        var pCurrentImprove = currentHalo + 1;

        var decrease = pCurrentImprove switch
        {
            >= 1 and <= 9 => 3,
            >= 10 and <= 19 => 6,
            >= 20 and <= 29 => 9,
            >= 30 and <= 39 => 12,
            >= 40 and <= 49 => 15,
            >= 50 and <= 59 => 18,
            >= 60 and <= 69 => 21,
            >= 70 and <= 79 => 24,
            >= 80 and <= 89 => 27,
            _ => 30
        };

        return (15, decrease);
    }

    /// <summary>
    ///     One full attempt: preconditions (CP/money/halo-cap) are the CALLER's job (<c>TribeActionHandler</c>,
    ///     which debits the fixed 100 CP / 1,000,000 money cost before calling this, matching the legacy's
    ///     unconditional debit). Consumes 1 or 2 draws from <paramref name="random" /> in the source's own
    ///     order (success roll, then -- only on failure -- the downgrade roll), per <see cref="IRandomSource" />'s
    ///     "one draw per legacy rand_mir() call site" contract.
    /// </summary>
    public static (TribeHaloEnchantOutcome Outcome, int NewHalo, int NewProtectForHalo) Resolve(
        int currentHalo, int currentProtectForHalo, IRandomSource random)
    {
        var (successRate, decreaseRate) = GetRates(currentHalo);
        // Quirk (doc 10 §2 tSort 7 / quirk table): +2 flat bonus applied AFTER the table lookup.
        var successThreshold = successRate + 2;

        if (random.NextInt32(100) < successThreshold)
            return (TribeHaloEnchantOutcome.Success, currentHalo + 1, currentProtectForHalo);

        if (random.NextInt32(100) < decreaseRate && currentHalo > 0)
        {
            if (currentProtectForHalo > 0)
                return (TribeHaloEnchantOutcome.ProtectionConsumed, currentHalo, currentProtectForHalo - 1);

            return (TribeHaloEnchantOutcome.Downgraded, currentHalo - 1, currentProtectForHalo);
        }

        return (TribeHaloEnchantOutcome.NeutralFail, currentHalo, currentProtectForHalo);
    }
}
