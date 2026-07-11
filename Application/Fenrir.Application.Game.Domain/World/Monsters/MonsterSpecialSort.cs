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
///         <b>Grounding / open question.</b> The contract describes each recipe's behavior in full but does
///         NOT contain the concrete <c>(Type, SpecialType) -&gt; sort</c> discriminator table itself: the
///         cited <c>ReturnSpecialSortNumber</c> body (<c>S10_MySummon.cpp:612-647</c>) sits in the contract's
///         "carried forward, not independently reopened this session" citation set. So only the mappings that
///         are independently grounded elsewhere are asserted here:
///         <list type="bullet">
///             <item>
///                 <b>Tribe-symbol stones -&gt; <see cref="TribeSymbolStone" /> (2).</b> The five
///                 <see cref="MonsterRowDto.SpecialType" /> values 11/12/13/28/14 are the "Holy Stone"
///                 guardians Fenrir already recognises by exactly these values in
///                 <see cref="MonsterSpawnScheduler" />'s own symbol-index map (its <c>TribeSymbolIndexOf</c>),
///                 and the contract's tribe-symbol recipe maps "five distinct special-type values ... to
///                 indices 0 through 4" -- the same five. This is the one non-default mapping with an
///                 independent Fenrir-side anchor, so it is reproduced; the resulting monsters are still a
///                 no-op in the dispatch until workstream A4 supplies their spawn objects.
///             </item>
///             <item>
///                 <b>Everything else -&gt; <see cref="Standard" /> (1).</b> The guard/tower/alliance/thrower
///                 discriminators (which <c>Type</c>/<c>SpecialType</c> values map to 5/10/4/6) are NOT
///                 available from this contract, so those monsters fall to the standard-mob default here rather
///                 than being guessed at -- flagged for a <c>legacy-behavior-translator</c> /
///                 <c>
///                     cpp-zone-
///                     gameplay-analyst
///                 </c>
///                 follow-up (reopen <c>ReturnSpecialSortNumber</c> and hand back the
///                 concrete table). Until then, the <see cref="CarThrower" />/<see cref="TribeGuard" />/etc.
///                 recipes in <see cref="MonsterAiSystem" /> are reachable only by an explicit
///                 <see cref="MonsterEntity.Create" /> <c>specialSort</c> override (tests), never by live
///                 derivation.
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
    ///     which fall to the <see cref="Standard" /> default pending the reopened <c>ReturnSpecialSortNumber</c>
    ///     table.
    /// </summary>
    public static byte Derive(byte type, byte specialType)
    {
        // Tribe-symbol "Holy Stone" guardians -- the one non-default mapping with an independent Fenrir anchor
        // (MonsterSpawnScheduler's own 11/12/13/28/14 -> 0..4 symbol map). Keyed on SpecialType only, matching
        // that same map's shape; Type is unused for this branch.
        _ = type;
        return specialType switch
        {
            11 or 12 or 13 or 28 or 14 => TribeSymbolStone,
            _ => Standard
        };
    }
}
