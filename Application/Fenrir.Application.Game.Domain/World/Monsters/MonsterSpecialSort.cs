namespace Fenrir.Application.Game.Domain.World.Monsters;

/// <summary>
///     Port of legacy <c>ReturnSpecialSortNumber</c> (<c>Server/ts25zone/S10_MySummon.cpp:612-647</c>): the
///     per-monster <c>mSpecialSortNumber</c> "archetype selector," a pure function of the monster's
///     <see cref="MonsterRowDto.Type" /> and <see cref="MonsterRowDto.SpecialType" />, computed once at spawn
///     (<see cref="MonsterEntity.SpecialSort" />) and read-only thereafter. It selects which idle/decision AI
///     recipe (<see cref="MonsterAiSystem" />'s <c>A002</c> switch) a monster runs.
/// </summary>
/// <remarks>
///     Behaviour-bearing archetype values (behavior-contract <c>A3-ai-recipes</c>, Inputs -&gt; "Archetype
///     selector"): <see cref="Standard" /> (1, the dominant path for nearly all monsters),
///     <see cref="TribeSymbolStone" /> (2), <see cref="Inert" /> (3, a fully inert recipe),
///     <see cref="AllianceStone" /> (4), <see cref="TribeGuard" /> (5), <see cref="CarThrower" /> (6), and
///     <see cref="Tower" /> (10). Any value outside that set is the recipe switch's inert default.
///     <para>
///         <b>UPDATE (2026-07-11, recovered <c>monster-ai-recipe-bodies</c> behavior contract).</b> This
///         session independently re-opened <c>S10_MySummon.cpp:612-647</c> (the complete
///         <c>ReturnSpecialSortNumber</c> body) rather than carrying forward the prior "not reopened"
///         citation, and recovered the FULL Type-based half of the discriminator that was previously missing:
///         <list type="bullet">
///             <item>
///                 <b><see cref="MonsterRowDto.Type" /> 6, 7, 8, or 9 -&gt; <see cref="TribeGuard" /> (5),
///                 unconditionally.</b> These four Type values resolve to Tribe Guard regardless of
///                 <see cref="MonsterRowDto.SpecialType" /> -- SpecialType is not inspected at all once Type
///                 matches one of them (checked FIRST in <see cref="Derive" />, ahead of the Type==1
///                 sub-switch, since the two conditions are mutually exclusive by construction). This closes
///                 <see cref="MonsterAiSystem" />'s own previously-documented gap ("<c>Derive</c> has no
///                 discriminator entry mapping a guard's (Type, SpecialType) to <see cref="TribeGuard" />") --
///                 a monster spawned by <see cref="World.ZoneWar.TribeGuardSpawner" /> with one of these Type
///                 values now really derives <see cref="TribeGuard" /> at spawn, so
///                 <see cref="MonsterAiSystem" />'s guard-recipe dispatch case is reachable in production, not
///                 test-override-only.
///             </item>
///             <item>
///                 <b>Type == 1, SpecialType 11/12/13/28/14/15 -&gt; <see cref="TribeSymbolStone" /> (2).</b>
///                 The recovered contract confirms six SpecialType codes map to this archetype under Type 1,
///                 not five: the five already-grounded via <see cref="MonsterSpawnScheduler" />'s own
///                 <c>TribeSymbolIndexOf</c> symbol-index map (11/12/13/28/14), PLUS a sixth code, 15, newly
///                 recovered this session. Code 15 still resolves to <see cref="TribeSymbolStone" /> at THIS
///                 selector level even though it is a genuine no-op one level deeper, inside the recipe body
///                 itself (the "Yaoguai"-adjacent sixth sub-type has no arm gate and no arm logic at all,
///                 per the companion Tribe-Symbol-Stone contract) -- the selector does not special-case 15
///                 away, the no-op lives inside the recipe, not the discriminator.
///             </item>
///             <item>
///                 <b>Type == 1, everything else, and every other Type outside {1,6,7,8,9} -&gt;
///                 <see cref="Standard" /> (1).</b> The recovered contract gives only the CARDINALITY of the
///                 remaining Type==1 SpecialType sets (one code -&gt; Tower, four -&gt; Inert, four -&gt;
///                 Alliance Stone, five -&gt; Car Thrower), never their concrete numeric values -- still NOT
///                 recoverable, so those monsters keep falling to the Standard default here rather than being
///                 guessed at. Flagged for a further <c>legacy-behavior-translator</c> /
///                 <c>cpp-zone-gameplay-analyst</c> follow-up (reopen <c>ReturnSpecialSortNumber</c>'s own
///                 SpecialType literals) -- not this session's to invent. Until then, the
///                 <see cref="Tower" />/<see cref="Inert" />/<see cref="AllianceStone" /> recipes in
///                 <see cref="MonsterAiSystem" /> remain reachable only via an explicit
///                 <see cref="MonsterEntity.Create" /> <c>specialSort</c> override (tests), never live
///                 derivation; <see cref="CarThrower" /> is the sole exception among the five remaining values, in
///                 that its recipe body is already fully implemented -- only its own live derivation is still
///                 blocked on these same unrecovered SpecialType literals.
///             </item>
///         </list>
///     </para>
///     <para>
///         Zone175-type bosses (<see cref="MonsterRowDto.SpecialType" /> 40-44) are dispatched by the update
///         router on <c>SpecialType</c> directly, ahead of and independent of this archetype selector (see
///         <see cref="MonsterAiSystem" />'s decision dispatcher), so their derived value here is never
///         consulted and is left at the <see cref="Standard" /> default.
///     </para>
/// </remarks>
public static class MonsterSpecialSort
{
    /// <summary>Standard mob -- the shared detection/aggro/targeting path (the default for nearly every monster).</summary>
    public const byte Standard = 1;

    /// <summary>Tribe-symbol "Holy Stone" (A4-spawned) -- resolves on its one-hour idle guard.</summary>
    public const byte TribeSymbolStone = 2;

    /// <summary>A fully inert recipe: no detection, no aggro, no action (contract "value 3").</summary>
    public const byte Inert = 3;

    /// <summary>Alliance stone (A4-spawned) -- resolves on its one-hour idle guard.</summary>
    public const byte AllianceStone = 4;

    /// <summary>Tribe guard (A4-spawned) -- direct-to-melee guard-attack acquisition.</summary>
    public const byte TribeGuard = 5;

    /// <summary>Car-thrower / ranged -- idle-wander vs. annulus ranged acquisition.</summary>
    public const byte CarThrower = 6;

    /// <summary>Tower (A4-spawned) -- periodic tower-attack hub action.</summary>
    public const byte Tower = 10;

    /// <summary>
    ///     Derives the archetype selector from a monster's template <paramref name="type" /> and
    ///     <paramref name="specialType" />. See the class remarks for exactly which mappings are grounded and
    ///     which fall to the <see cref="Standard" /> default pending a further reopening of
    ///     <c>ReturnSpecialSortNumber</c>'s own remaining SpecialType literals.
    /// </summary>
    public static byte Derive(byte type, byte specialType)
    {
        // Tribe-Guard Type codes (S10_MySummon.cpp:612-647, recovered 2026-07-11): four fixed Type values ->
        // Tribe Guard, unconditionally -- SpecialType is not inspected at all once Type matches one of these,
        // so this check runs FIRST, ahead of the Type==1 sub-switch below (the two are mutually exclusive by
        // construction: a monster's Type is exactly one value).
        if (type is 6 or 7 or 8 or 9)
            return TribeGuard;

        if (type != 1)
            return Standard; // every Type outside {1,6,7,8,9} -> Standard default, SpecialType never inspected

        // Type == 1: SpecialType-keyed sub-switch. Only the tribe-symbol "Holy Stone" set is grounded -- the
        // five special types MonsterSpawnScheduler's own 11/12/13/28/14 -> 0..4 symbol map already anchors,
        // PLUS a sixth code (15) recovered this session (still resolves to this archetype at the SELECTOR
        // level even though it is a genuine no-op one level deeper, inside the recipe body itself). The
        // Tower/Inert/Alliance-Stone/Car-Thrower SpecialType sets remain unrecovered (only their cardinality
        // is known, not their concrete values) -- flagged, not guessed; every unmatched SpecialType falls to
        // the shared Standard default below.
        return specialType switch
        {
            11 or 12 or 13 or 28 or 14 or 15 => TribeSymbolStone,
            _ => Standard
        };
    }
}
