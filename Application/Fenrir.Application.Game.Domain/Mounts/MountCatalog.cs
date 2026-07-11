using System.Collections.Frozen;

namespace Fenrir.Application.Game.Domain.Mounts;

/// <summary>
///     Mount-catalog validity: which item ids the legacy recognizes as a rideable mount (<c>IsValidMount</c> and
///     its <c>IsValidMount5/10/15/20</c> tier hierarchy). Read-only reference data built once. Not wired as a
///     gate anywhere yet -- no mount-acquisition path exists in Fenrir today (the only populators of the mount
///     garage are the not-yet-implemented mount-item-use branches), so this exists to capture the parts of the
///     catalog the behavior contract could ground, and to be the drop-in home for the rest once a follow-up
///     contract supplies the still-missing tier member ids.
/// </summary>
/// <remarks>
///     Réf. C++ : Server/Header/function.h:2305-2418 (<c>IsValidMount5/10/15/20</c> tier predicates and the
///     aggregate <c>IsValidMount</c>) ; Server/Header/Protocol/DEFINE.h:116-117 (<c>GIFT_EVENT</c> is defined
///     unconditionally -- the adjacent <c>//#define GIFT_V2</c> is commented out -- so the gift-event mount
///     range 8301-8331 IS valid in production, consistent with the 2026-07-10 exhaustive re-qualification of
///     <c>GIFT_EVENT</c> from wrongly-dead to live) ; Server/Header/Protocol/DEFINE.h:156-183 (the 27-id
///     <c>ANIMAL_NUM_*</c> block backing the tier hierarchy) ; Server/Header/function.h:2339-2354
///     (<c>IsValidMount15</c>, the tier-3 gate) ; Server/Header/function.h:2356-2366 (<c>IsValidMount20</c>, the
///     Puma-tier gate) ; Server/Header/function.h:2368-2418 (the aggregate's own match list at 2370-2402, the
///     gift-event/19002-19011 range checks at 2405-2411, and the two-step fall-through at 2413-2417: the tier-3
///     gate is tried first, and only if it yields no match is the Puma-tier gate tried as the final fallback).
///     <para>
///         GROUNDED HERE (id ranges/sets the contract transcribed verbatim, or cross-verified against an
///         already-merged, independently-recovered C# source citing the same <c>ANIMAL_NUM_*</c> block):
///         single id 559, the 1332-1341 range, the <c>GIFT_EVENT</c> 8301-8331 range, and the 19002-19011
///         range (all accepted by <see cref="IsRecognizedMount" /> directly, as part of the aggregate's own
///         match list/range checks) ; the eight tier-3 (<c>IsValidMount15</c>) ids -- see
///         <see cref="Tier3Ids" />, cross-checked against
///         <see cref="Fenrir.Application.Game.Domain.World.Loot.MountBox635RewardTable.RewardItemIds" />'s
///         independently-recovered pool of the same eight ids -- reachable from the aggregate only via the
///         first fall-through step (not listed in the aggregate's own match list) ; the single known
///         Puma-tier (<c>IsValidMount20</c>) id, <see cref="Puma3Id" /> (1331, <c>ANIMAL_NUM_PUMA3</c>) --
///         reachable from the aggregate only via the second, final fall-through step (reached specifically
///         because the first fall-through, to the tier-3 gate, fails to match it).
///     </para>
///     <para>
///         GAP (flagged, NOT invented): the eight tier-1 ids (<c>IsValidMount5</c> -- the same eight animals'
///         first-grade variant, e.g. <c>ANIMAL_NUM_TIGER1</c> = 1301 per <c>game.Characters.MountItemId</c>'s
///         own comment, but its seven tier-1 siblings are not independently confirmed as a complete,
///         contract-grounded set), the eight tier-2 ids (<c>IsValidMount10</c>, second-grade variant), the
///         remaining two Puma variants (Puma1/Puma2 -- <see cref="Puma3Id" /> is the only one of the three
///         Puma-tier ids grounded so far), and the standalone Christmas-mount id (recognized ONLY by the
///         aggregate's own match list, not a member of any tier gate) were NOT transcribed into the behavior
///         contract with a concrete, cross-verifiable numeric value -- only their tier grouping, counts, and
///         fall-through position were. Per this codebase's hard rule against inventing item ids, they are
///         absent here rather than guessed. Consequently <see cref="IsRecognizedMount" /> is still a PARTIAL
///         recognizer: a true result is authoritative, but a false result does NOT prove an id is not a mount
///         (e.g. 1301 is a real tier-1 mount and still returns false here). Do not use this as a rejection
///         gate until a follow-up contract supplies the rest of the tier member id table.
///     </para>
/// </remarks>
public static class MountCatalog
{
    /// <summary>Inclusive bounds of the <c>GIFT_EVENT</c> gift-event mount range (live in production).</summary>
    public const int GiftEventMinId = 8301;

    /// <summary>Inclusive bounds of the <c>GIFT_EVENT</c> gift-event mount range (live in production).</summary>
    public const int GiftEventMaxId = 8331;

    /// <summary>
    ///     <c>ANIMAL_NUM_PUMA3</c>: the only one of the three Puma-tier (<c>IsValidMount20</c>) ids grounded so
    ///     far -- cross-verified against
    ///     <see cref="Fenrir.Application.Game.Domain.World.Loot.MountBox635RewardTable" />'s own remarks, which
    ///     independently confirm 1331 as <c>ANIMAL_NUM_PUMA3</c> from the same <c>DEFINE.h</c> constant block.
    ///     Puma1/Puma2 remain an open GAP -- see this type's own remarks.
    /// </summary>
    public const int Puma3Id = 1331;

    /// <summary>The standalone mount ids the aggregate accepts by exact match (contract-transcribed subset).</summary>
    private static readonly FrozenSet<int> StandaloneIds = new[] { 559 }.ToFrozenSet();

    /// <summary>
    ///     The eight tier-3 (<c>IsValidMount15</c>) mount ids -- the third-grade variant of Tiger, Pig, Deer,
    ///     Bear, Cat, Bull, Wolf, and Lion, per Server/Header/function.h:2339-2354 and the
    ///     <c>ANIMAL_NUM_*</c> block, Server/Header/Protocol/DEFINE.h:156-183. Cross-verified against the
    ///     independently-recovered
    ///     <see cref="Fenrir.Application.Game.Domain.World.Loot.MountBox635RewardTable.RewardItemIds" /> pool
    ///     (same eight ids, same source constants) -- the two independently-derived sources agree exactly.
    /// </summary>
    private static readonly FrozenSet<int> Tier3Ids = new[]
    {
        1307, // Tiger3
        1308, // Pig3
        1309, // Deer3
        1315, // Bear3
        1319, // Cat3
        1322, // Bull3
        1325, // Wolf3
        1328 // Lion3
    }.ToFrozenSet();

    /// <summary>True for the gift-event mount range 8301-8331 (<c>GIFT_EVENT</c> defined -> live in production).</summary>
    public static bool IsGiftEventMount(int itemId)
    {
        return itemId is >= GiftEventMinId and <= GiftEventMaxId;
    }

    /// <summary>
    ///     True for the eight tier-3 (<c>IsValidMount15</c>) mount ids -- see <see cref="Tier3Ids" />. A true
    ///     result is authoritative; this tier's set is fully grounded (all eight members known), unlike the
    ///     still-partial tier-1/tier-2/Puma-tier sets.
    /// </summary>
    public static bool IsTier3Mount(int itemId)
    {
        return Tier3Ids.Contains(itemId);
    }

    /// <summary>
    ///     True for the mount-id ranges/sets the behavior contract transcribed verbatim or cross-verified
    ///     (single id 559, 1332-1341, gift-event 8301-8331, 19002-19011, the eight tier-3 ids reachable via the
    ///     aggregate's first fall-through, and <see cref="Puma3Id" /> reachable via the aggregate's second,
    ///     final fall-through). A true result is authoritative; a false result is NOT -- see this type's own
    ///     GAP remarks for the tier-1/tier-2/Puma1/Puma2/Christmas-mount ids not yet supplied.
    /// </summary>
    public static bool IsRecognizedMount(int itemId)
    {
        return StandaloneIds.Contains(itemId)
               || itemId is >= 1332 and <= 1341
               || IsGiftEventMount(itemId)
               || itemId is >= 19002 and <= 19011
               || IsTier3Mount(itemId)
               || itemId == Puma3Id;
    }
}
