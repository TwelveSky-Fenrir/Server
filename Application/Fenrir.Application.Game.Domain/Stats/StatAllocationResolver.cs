namespace Fenrir.Application.Game.Domain.Stats;

/// <summary>
///     Pure resolver for tSort 206 (<c>ProcessForStatPlus</c>): spends unspent stat points (aStatPoint) to raise
///     one of the four base attributes. No I/O and no dependency on <c>Zone</c>/<c>PlayerRuntimeState</c> -- the
///     caller supplies the character's current stat-points balance and gets back which attribute to credit, by
///     how much, and the resulting balance.
/// </summary>
/// <remarks>
///     Réf. C++ : Server/ts25zone/S04_MyWork05.cpp:705-791 (<c>ProcessForStatPlus</c> -- the three affordability
///     regimes, the attribute-selection switch and its Strength/Dexterity/Vitality/Intelligence mapping, and the
///     debit logic) ; Server/ts25zone/S04_MyWork04.cpp:303-306,354,420-428 (dispatcher routing tSort 206 here,
///     and the immediate return with no acknowledgment on failure) ; Server/Header/Protocol/STRUCT.h:1261-1265
///     (<c>STAT_PLUS_RECV</c> wire payload: <c>tStatSort</c>, <c>tAddValue</c>) ;
///     Server/Header/Protocol/STRUCT.h:347-353 (<c>aVit</c>/<c>aStr</c>/<c>aInt</c>/<c>aDex</c>/<c>aStatPoint</c>
///     are plain 32-bit ints, no declared maximum-value field alongside them -- see <see cref="Result" />'s own
///     remarks on overflow). Every rejection below (illegal category code, or a legal one the character cannot
///     currently afford) is signaled identically by the caller: a session disconnect, never a soft failure reply
///     -- see <see cref="Outcome.Disconnect" />.
/// </remarks>
public static class StatAllocationResolver
{
    /// <summary>The four base attributes a category code may credit -- Str/Dex/Vit/Int order per the legacy switch.</summary>
    public enum BaseStat
    {
        Strength,
        Dexterity,
        Vitality,
        Intelligence
    }

    public enum Outcome
    {
        /// <summary>
        ///     Illegal category code, or a legal one the character cannot currently afford -- the caller must
        ///     disconnect the session for this request rather than reply with any failure code.
        /// </summary>
        Disconnect,

        Success
    }

    /// <summary>tStatSort 1-4: fixed regime, +1 to Str/Dex/Vit/Int respectively, at a fixed cost of 1 stat point.</summary>
    public const int SmallFixedCategoryMin = 1;

    public const int SmallFixedCategoryMax = 4;

    /// <summary>tStatSort 5-8: fixed regime, +5 to Str/Dex/Vit/Int respectively, at a fixed cost of 5 stat points.</summary>
    public const int LargeFixedCategoryMin = 5;

    public const int LargeFixedCategoryMax = 8;

    /// <summary>tStatSort 9-12: variable regime, +tAddValue to Str/Dex/Vit/Int respectively, cost == tAddValue.</summary>
    public const int VariableCategoryMin = 9;

    public const int VariableCategoryMax = 12;

    /// <summary>
    ///     Resolves one CZ_PROCESS_DATA_SEND tSort-206 request. <paramref name="addValue" /> is read only for
    ///     category codes 9-12 -- for 1-8 it is accepted on the wire but has zero effect on the outcome,
    ///     matching the legacy's own unread field for that range. A <paramref name="statSort" /> outside 1-12
    ///     is rejected the same outward way as an unaffordable amount (nothing mutated, caller disconnects),
    ///     even though no affordability check in this method actually runs for it -- each affordability check
    ///     is scoped only to its own regime's sub-range, and an out-of-range code falls straight through every
    ///     one of them to the "no matching attribute" case instead (mirrors the legacy's own unmatched-selector
    ///     fallthrough, S04_MyWork05.cpp:705-791).
    /// </summary>
    /// <param name="statSort">tStatSort, the wire category code -- legal range 1-12.</param>
    /// <param name="addValue">tAddValue, only meaningful for category codes 9-12.</param>
    /// <param name="currentStatPoints">The character's current unspent stat-points balance (aStatPoint).</param>
    public static Result Resolve(int statSort, int addValue, int currentStatPoints)
    {
        return statSort switch
        {
            1 => ResolveFixed(BaseStat.Strength, 1, currentStatPoints),
            2 => ResolveFixed(BaseStat.Dexterity, 1, currentStatPoints),
            3 => ResolveFixed(BaseStat.Vitality, 1, currentStatPoints),
            4 => ResolveFixed(BaseStat.Intelligence, 1, currentStatPoints),
            5 => ResolveFixed(BaseStat.Strength, 5, currentStatPoints),
            6 => ResolveFixed(BaseStat.Dexterity, 5, currentStatPoints),
            7 => ResolveFixed(BaseStat.Vitality, 5, currentStatPoints),
            8 => ResolveFixed(BaseStat.Intelligence, 5, currentStatPoints),
            9 => ResolveVariable(BaseStat.Strength, addValue, currentStatPoints),
            10 => ResolveVariable(BaseStat.Dexterity, addValue, currentStatPoints),
            11 => ResolveVariable(BaseStat.Vitality, addValue, currentStatPoints),
            12 => ResolveVariable(BaseStat.Intelligence, addValue, currentStatPoints),
            _ => Result.Disconnect
        };
    }

    private static Result ResolveFixed(BaseStat stat, int amount, int currentStatPoints)
    {
        return currentStatPoints < amount
            ? Result.Disconnect
            : new Result(Outcome.Success, stat, amount, currentStatPoints - amount);
    }

    private static Result ResolveVariable(BaseStat stat, int addValue, int currentStatPoints)
    {
        return addValue < 1 || addValue > currentStatPoints
            ? Result.Disconnect
            : new Result(Outcome.Success, stat, addValue, currentStatPoints - addValue);
    }

    /// <param name="Outcome">
    ///     <see cref="StatAllocationResolver.Outcome.Disconnect" /> means the caller must terminate the
    ///     session for this request; nothing described by the other fields should be applied.
    /// </param>
    /// <param name="Stat">
    ///     Which base attribute to credit -- meaningless when <see cref="Outcome" /> is
    ///     <see cref="StatAllocationResolver.Outcome.Disconnect" />.
    /// </param>
    /// <param name="Amount">
    ///     Both the amount credited to <see cref="Stat" /> and the amount debited from the stat-points balance.
    ///     No overflow guard is applied here on the destination attribute, matching the legacy's own lack of one
    ///     for this regime (unlike money-transfer-style behaviors elsewhere in the same source file that widen to
    ///     a 64-bit intermediate first) -- unverified whether this is practically reachable, not assumed-safe.
    /// </param>
    /// <param name="NewStatPoints">The character's stat-points balance after the debit.</param>
    public readonly record struct Result(Outcome Outcome, BaseStat Stat, int Amount, int NewStatPoints)
    {
        public bool Succeeded => Outcome == Outcome.Success;

        public static Result Disconnect { get; } = new(Outcome.Disconnect, default, 0, 0);
    }
}
