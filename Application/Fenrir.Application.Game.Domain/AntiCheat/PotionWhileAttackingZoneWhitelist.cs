using System.Collections.Frozen;

namespace Fenrir.Application.Game.Domain.AntiCheat;

/// <summary>
///     The 40-zone hand-curated list <c>CheckPossibleEatPotionAttack</c> returns a positive indicator for,
///     zero for every other zone (<c>Server/ts25zone/S04_MyWork05.cpp:4995-5071</c>). A pure lookup, no I/O.
/// </summary>
/// <remarks>
///     <b>Semantics deliberately NOT wired.</b> The D2 contract flags that
///     <b>
///         the call site and the exact
///         meaning of the indicator are not present in the cited range
///     </b>
///     — whether a listed zone <i>permits</i>
///     potion use during an attack or <i>flags</i> it as suspicious is unresolved. The method is therefore
///     named neutrally (<see cref="IsListed" />): it answers only "is this zone on the legacy list," never
///     "may this action proceed." Do not consume this from any potion/consumable path until
///     <c>cpp-zone-gameplay-analyst</c> has cited the call site and the permit-vs-flag polarity — wiring it
///     with the wrong polarity would either silently disable a real anti-cheat gate or wrongly block a
///     legitimate action in 40 zones.
///     <para>
///         The list is data, not a derivable rule: many further ids appear commented-out in the legacy switch,
///         confirming a hand-maintained, evolving set. The exact ids are transcribed verbatim from the source,
///         never inferred.
///     </para>
/// </remarks>
public static class PotionWhileAttackingZoneWhitelist
{
    private static readonly FrozenSet<int> ListedZones = new[]
    {
        1, 2, 3, 4, 6, 7, 8, 9, 11, 12, 13, 14, 38, 55, 75, 85, 86, 87, 89, 90,
        99, 100, 125, 140, 141, 142, 143, 195, 196, 197, 198, 199, 201, 270, 271, 272, 273, 274, 295, 296
    }.ToFrozenSet();

    /// <summary>
    ///     The count of listed zones — 40, per the cited range. Exposed for the regression test that pins the
    ///     transcription.
    /// </summary>
    public static int Count => ListedZones.Count;

    /// <summary>
    ///     True when <paramref name="zoneId" /> is one of the 40 listed zones. Says nothing about whether an action is
    ///     permitted — see the type remarks.
    /// </summary>
    public static bool IsListed(int zoneId)
    {
        return ListedZones.Contains(zoneId);
    }
}
