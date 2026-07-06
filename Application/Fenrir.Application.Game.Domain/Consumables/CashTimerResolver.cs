using Fenrir.Application.Game.Stats;

namespace Fenrir.Application.Game.Domain.Consumables;

/// <summary>
///     The two banked cash-timer counters from this family with an unambiguous single catalog id each --
///     Faction Notice Scroll and Taiyan Key. The rest of the family (Ivy Hall, proxy-shop extension, Animal
///     Double Exp/Absorb, Protection Scroll, the Damage/HP/Critical Boost trio) shares this exact per-id "add
///     a fixed amount, ceiling-checked" shape but was left out of this pass: the originating behavior contract
///     could only place their exact catalog ids inside larger, ambiguously-delimited id groups, and guessing
///     which id maps to which mechanic would risk a silent wrong-amount/wrong-counter bug -- flagged as a
///     follow-up needing a fresh legacy-behavior-translator pass with the precise per-id table.
/// </summary>
/// <remarks>
///     Réf. C++ : Server/ts25zone/S04_MyWork03.cpp:2622-2630 (Faction Notice Scroll, <c>aTribeNotifyNum</c>) ;
///     :3712-3726 (Taiyan Key, <c>aZone125Time</c>, level-cap precondition -- disconnects rather than failing
///     cleanly, the one exception in this whole family) ; :106-117 (<c>wCheckAdd</c>/<c>wCheckAdd2</c>, the
///     plain 32-bit ceiling-check style this family uses).
/// </remarks>
public static class CashTimerResolver
{
    public const int FactionNoticeAddAmount = 5;
    public const int TaiyanKeyAddAmount = 180;

    public enum Outcome
    {
        Success,

        /// <summary>Adding would exceed the shared 2,000,000,000 ceiling -- clean failure, counter untouched.</summary>
        WouldExceedCeiling,

        /// <summary>
        ///     Taiyan Key only: base level not at the level cap. Unlike every other precondition failure in this
        ///     family, the legacy disconnects the session for this one rather than answering with a clean
        ///     failure -- the caller must map this outcome to a disconnect, not a normal response.
        /// </summary>
        LevelCapNotMet
    }

    public static Result ResolveFactionNotice(int currentCount)
    {
        var added = BankedCounterMath.AddNarrow(currentCount, FactionNoticeAddAmount);
        return added.Succeeded
            ? new Result(Outcome.Success, added.NewValue)
            : new Result(Outcome.WouldExceedCeiling, currentCount);
    }

    public static Result ResolveTaiyanKey(short characterLevel, int currentTimer)
    {
        if (characterLevel < LevelProgressionCalculator.MaxLevel)
            return new Result(Outcome.LevelCapNotMet, currentTimer);

        var added = BankedCounterMath.AddNarrow(currentTimer, TaiyanKeyAddAmount);
        return added.Succeeded
            ? new Result(Outcome.Success, added.NewValue)
            : new Result(Outcome.WouldExceedCeiling, currentTimer);
    }

    public readonly record struct Result(Outcome Outcome, int NewValue)
    {
        public bool Succeeded => Outcome == Outcome.Success;
    }
}
