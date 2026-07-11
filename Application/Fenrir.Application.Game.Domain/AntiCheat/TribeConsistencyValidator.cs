namespace Fenrir.Application.Game.Domain.AntiCheat;

/// <summary>
///     Avatar-registration tribe/previous-tribe self-consistency guard — the check ts25 runs on the stored
///     <c>aTribe</c>/<c>aPreviousTribe</c> pair before admitting an avatar to the world, disconnecting the
///     session on any inconsistent pair (<c>Server/ts25zone/S04_MyWork02.cpp:880-901</c>). A pure table check:
///     no I/O, no shared state.
/// </summary>
/// <remarks>
///     Legal combinations (every other pair is illegal → the caller must tear the session down, not merely
///     reject the entry):
///     <list type="bullet">
///         <item>
///             <description><c>Tribe</c> in {0,1,2} (a main faction) requires <c>PreviousTribe</c> equal to <c>Tribe</c>.</description>
///         </item>
///         <item>
///             <description>
///                 <c>Tribe</c> == 3 (Fujin, the fourth tribe) requires <c>PreviousTribe</c> in {0,1,2} — the
///                 faction it converted from.
///             </description>
///         </item>
///     </list>
///     Fenrir already gates world entry on this at <c>EnterWorldService</c> (see the citation echoed in
///     <c>Zone.PlayerLifecycle.HandleEnter</c>'s <c>PreviousTribe</c> remark); this type factors the rule into
///     one testable, reusable predicate so any future path that (re)admits an avatar — a zone transfer, a
///     tribe-conversion (op37) mid-session — validates it identically rather than re-deriving the combinations.
/// </remarks>
public static class TribeConsistencyValidator
{
    /// <summary>
    ///     True when <paramref name="tribe" />/<paramref name="previousTribe" /> form a legal pair. False means
    ///     the caller must disconnect the session (the "hostile/tampered input" sanction, matching the legacy
    ///     <c>Quit</c> at <c>:887,894,899</c>).
    /// </summary>
    public static bool IsConsistent(int tribe, int previousTribe)
    {
        return tribe switch
        {
            0 or 1 or 2 => previousTribe == tribe,
            3 => previousTribe is 0 or 1 or 2,
            _ => false
        };
    }
}
