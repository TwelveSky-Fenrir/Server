using Fenrir.Application.Game.Combat;

namespace Fenrir.Application.Game.Tribes;

/// <summary>Outcome of one TRIBE_WORK tSort 7 halo-enchant attempt (ZC_TRIBE_WORK_RECV tResult 0/1/2).</summary>
public enum TribeHaloEnchantOutcome
{
    /// <summary>tResult 0 -- aHalo += 1.</summary>
    Success,

    /// <summary>tResult 1 -- a consumable aProtectForHalo charge absorbed the downgrade.</summary>
    ProtectionConsumed,

    /// <summary>tResult 2 -- aHalo -= 1.</summary>
    Downgraded,

    /// <summary>tResult 1 -- neither success nor downgrade; also the only outcome possible at aHalo==0.</summary>
    NeutralFail
}

/// <summary>
///     Port of GetHaloCostumeEnchantRate (function.h:2165-2214) plus the two-roll decision tree around it
///     (S04_MyWork02.cpp:11128-11230, TRIBE_WORK tSort 7). No I/O -- TribeActionHandler supplies the current
///     aHalo/aProtectForHalo and reads the result back.
/// </summary>
public static class TribeHaloEnchantResolver
{
    /// <summary>
    ///     currentHalo is aHalo (the C++ adds 1 internally, "pCurrentImprove"). SuccessRate is a flat 15
    ///     regardless of tier; only DecreaseRate varies, +3 per 10-point bracket, up to 30 at 90+. Both are
    ///     percentage points for a rand() % 100 roll.
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
    ///     Preconditions (CP/money/halo-cap) are the caller's job (TribeActionHandler debits the fixed cost
    ///     before calling this). Consumes 1 or 2 draws from <paramref name="random" /> in source order:
    ///     success roll, then -- only on failure -- the downgrade roll.
    /// </summary>
    public static (TribeHaloEnchantOutcome Outcome, int NewHalo, int NewProtectForHalo) Resolve(
        int currentHalo, int currentProtectForHalo, IRandomSource random)
    {
        var (successRate, decreaseRate) = GetRates(currentHalo);
        var successThreshold = successRate + 2; // +2 flat bonus applied after the table lookup

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
