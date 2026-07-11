namespace Fenrir.Application.Game.Domain.World.WorldState;

/// <summary>
///     Pure eligibility gates (b)-(d) of the five-gate chain a tribe's Force Leader must clear before their
///     "Tribe Work" sub-command-5 request is allowed to arm that tribe's world-scope Formation ability slot
///     (<see cref="WorldStateService.SetTribeFormationAbility" />) -- gate (a) (all four tribes' points above
///     the floor) is included here too since it shares the same <see cref="TribeRvrState" /> input and the
///     same "pure, no I/O" shape; gate (d) (Tribe Symbol Battle active) is a single-field read against
///     <see cref="WorldStateService.World" /> and is therefore left to the caller rather than duplicated here.
///     See <see cref="Fenrir.Application.Game.Services.Tribes.TribeActionService.ValidateTribeSkill" /> for the
///     full five-gate call order (Force-Leader-role and payload-range first, then (a)-(d) here, in order).
/// </summary>
public static class TribeFormationAbilityEligibility
{
    /// <summary>
    ///     Gate (a)'s floor: a tribe's own point total must exceed this value (101+ passes, exactly 100
    ///     fails) for <see cref="AllTribesAboveFloor" /> to hold across every tribe.
    /// </summary>
    public const int PointFloor = 100;

    /// <summary>
    ///     Gate (c)'s share ceiling, in whole percent: a computed share AT OR ABOVE this value fails; strictly
    ///     under it passes.
    /// </summary>
    public const int SharePercentThreshold = 20;

    /// <summary>
    ///     Gate (a): a global floor across all four tribes, not scoped to any one requester -- every tribe's
    ///     current point total must exceed <see cref="PointFloor" />.
    /// </summary>
    /// <remarks>Réf. C++ : Server/ts25zone/S04_MyWork02.cpp:11067-11071 ; Server/Header/Protocol/DEFINE.h:309 (MAX_TRIBE_NUM = 4).</remarks>
    public static bool AllTribesAboveFloor(IReadOnlyList<TribeRvrState> tribes)
    {
        foreach (var tribe in tribes)
            if (tribe.Points <= PointFloor)
                return false;

        return true;
    }

    /// <summary>
    ///     Gate (b): the single tribe currently holding the lowest cumulative point total. Tie-break: strict
    ///     less-than comparison evaluated in tribe-id order starting from tribe 0 as the initial candidate --
    ///     on an exact tie, the lowest tribe id among the tied tribes wins; a later-id tribe never displaces an
    ///     earlier one it merely ties.
    /// </summary>
    /// <remarks>
    ///     Réf. C++ : Server/ts25zone/S04_MyWork02.cpp:11072-11077 (the gate itself) ; Server/Header/function.h:
    ///     3145-3158 (the tie-break loop: strict less-than, tribe 0 initial candidate, MAX_TRIBE_NUM-bounded).
    /// </remarks>
    public static byte FindLowestPointTribe(IReadOnlyList<TribeRvrState> tribes)
    {
        var lowest = tribes[0];
        for (var i = 1; i < tribes.Count; i++)
            if (tribes[i].Points < lowest.Points)
                lowest = tribes[i];

        return lowest.TribeId;
    }

    /// <summary>Sum of every tribe's current point total -- the divisor gate (c) needs, and gate (a)'s own floor guarantees non-degenerate once gate (a) has passed.</summary>
    public static int CombinedPoints(IReadOnlyList<TribeRvrState> tribes)
    {
        var total = 0;
        foreach (var tribe in tribes)
            total += tribe.Points;

        return total;
    }

    /// <summary>
    ///     Gate (c): <paramref name="ownPoints" /> as a percentage of <paramref name="combinedPoints" />, using
    ///     integer (truncating-toward-zero) division. A computed share of <see cref="SharePercentThreshold" />
    ///     or higher fails; strictly under it passes. Must never be called with a
    ///     <paramref name="combinedPoints" /> of zero -- callers are required to have already confirmed gate
    ///     (a) passed, which guarantees this divisor can never be zero/degenerate.
    /// </summary>
    /// <remarks>
    ///     Réf. C++ : Server/ts25zone/S04_MyWork02.cpp:11078-11083 (the gate itself, including an inline
    ///     worked-example comment: 6467 out of a combined 30714, illustrating the calculation's shape, not a
    ///     live value).
    /// </remarks>
    public static bool IsUnderShareThreshold(int ownPoints, int combinedPoints)
    {
        var sharePercent = ownPoints * 100 / combinedPoints;
        return sharePercent < SharePercentThreshold;
    }
}
