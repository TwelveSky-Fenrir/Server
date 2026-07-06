namespace Fenrir.Application.Game.Domain.Consumables;

/// <summary>
///     Stats Clear (full four-stat reset) and Stat Cleanse (single-stat reset) scroll families. No I/O, no
///     Zone dependency -- the caller supplies the character's current stat values/level/rebirth count already
///     read off <c>PlayerRuntimeState</c>, and separately recomputes derived combat stats/HP-MP from the
///     result (this type only computes the refund/floor-reset math).
/// </summary>
/// <remarks>
///     Réf. C++ : Server/ts25zone/S04_MyWork03.cpp:4249-4298 (Stats Clear -- id-to-level-band table, full
///     four-stat refund-and-floor) ; :4301-4391 (Stat Cleanse -- id-to-level-band table, target-selector range
///     check, per-stat refund-and-floor branch). The level-band boundary semantics (inclusive/exclusive) were
///     transcribed from two structurally-similar-but-not-textually-identical switch blocks in the source and
///     were flagged in the originating behavior contract as needing direct re-confirmation against
///     Server/ts25zone/S04_MyWork03.cpp before this ships to production; <see cref="TryResolveLevelBand" />
///     intentionally returns false (no confident band) for the level range those two citations did not
///     unambiguously cover (base level 113-144 with at least one rebirth) rather than guessing which of the
///     two adjacent bands it should fall into.
/// </remarks>
public static class StatResetResolver
{
    public const int StatFloor = 1;

    /// <summary>Stat Cleanse's target-stat selector -- exactly one of these four values is valid input.</summary>
    public enum StatSelector
    {
        Strength = 1,
        Dexterity = 2,
        Vitality = 3,
        Intelligence = 4
    }

    public enum LevelBand
    {
        UpTo99,
        Level100To112,
        Level113PlusNoRebirth,
        Level145PlusWithRebirth
    }

    public enum CleanseOutcome
    {
        Success,

        /// <summary>The targeted stat is already at <see cref="StatFloor" /> -- nothing to refund.</summary>
        AlreadyAtFloor
    }

    /// <summary>
    ///     See this type's own remarks: the ambiguous mid-range (base level 113-144 with at least one rebirth)
    ///     deliberately returns false rather than guessing which adjacent band applies.
    /// </summary>
    public static bool TryResolveLevelBand(short level, int rebirthCount, out LevelBand band)
    {
        switch (level)
        {
            case <= 99:
                band = LevelBand.UpTo99;
                return true;
            case <= 112:
                band = LevelBand.Level100To112;
                return true;
            case >= 145 when rebirthCount >= 1:
                band = LevelBand.Level145PlusWithRebirth;
                return true;
            case >= 113 when rebirthCount == 0:
                band = LevelBand.Level113PlusNoRebirth;
                return true;
            default:
                band = default;
                return false;
        }
    }

    /// <summary>Every above-floor point across all four stats is refunded; all four reset to <see cref="StatFloor" />.</summary>
    public static ClearResult ResolveStatsClear(int statVit, int statStr, int statInt, int statDex)
    {
        var refund = AboveFloor(statVit) + AboveFloor(statStr) + AboveFloor(statInt) + AboveFloor(statDex);
        return new ClearResult(StatFloor, StatFloor, StatFloor, StatFloor, refund);
    }

    /// <summary>Only the selected stat is reset/refunded; the other three are left untouched by the caller.</summary>
    public static CleanseResult ResolveStatCleanse(int currentValue)
    {
        if (currentValue <= StatFloor)
            return new CleanseResult(CleanseOutcome.AlreadyAtFloor, currentValue, 0);

        return new CleanseResult(CleanseOutcome.Success, StatFloor, AboveFloor(currentValue));
    }

    private static int AboveFloor(int value)
    {
        return Math.Max(0, value - StatFloor);
    }

    public readonly record struct ClearResult(int NewStatVit, int NewStatStr, int NewStatInt, int NewStatDex,
        int RefundedPoints);

    public readonly record struct CleanseResult(CleanseOutcome Outcome, int NewValue, int RefundedPoints)
    {
        public bool Succeeded => Outcome == CleanseOutcome.Success;
    }
}
