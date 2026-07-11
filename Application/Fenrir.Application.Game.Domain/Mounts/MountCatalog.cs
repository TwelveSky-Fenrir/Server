using System.Collections.Frozen;

namespace Fenrir.Application.Game.Domain.Mounts;

/// <summary>
///     Mount-catalog validity: which item ids the legacy recognizes as a rideable mount (<c>IsValidMount</c> and
///     its <c>IsValidMount5/10/15/20</c> tier hierarchy). Read-only reference data built once. Not wired as a
///     gate anywhere yet -- no mount-acquisition path exists in Fenrir today (the only populators of the mount
///     garage are the not-yet-implemented mount-item-use branches), so this exists to capture the parts of the
///     catalog the behavior contract could ground, and to be the drop-in home for the rest once a follow-up
///     contract supplies the tier member ids.
/// </summary>
/// <remarks>
///     Réf. C++ : Server/Header/function.h:2305-2418 (<c>IsValidMount5/10/15/20</c> tier predicates and the
///     aggregate <c>IsValidMount</c>) ; Server/Header/Protocol/DEFINE.h:116-117 (<c>GIFT_EVENT</c> is defined
///     unconditionally -- the adjacent <c>//#define GIFT_V2</c> is commented out -- so the gift-event mount
///     range 8301-8331 IS valid in production, consistent with the 2026-07-10 exhaustive re-qualification of
///     <c>GIFT_EVENT</c> from wrongly-dead to live).
///     <para>
///         GROUNDED HERE (id ranges the contract transcribed verbatim): single id 559, the 1332-1341 range, the
///         <c>GIFT_EVENT</c> 8301-8331 range, and the 19002-19011 range -- all accepted by
///         <see cref="IsRecognizedMount" />.
///     </para>
///     <para>
///         GAP (flagged, NOT invented): the eight tier-1 ids (<c>IsValidMount5</c>), eight tier-2
///         (<c>IsValidMount10</c>), eight tier-3 (<c>IsValidMount15</c>), the three Puma variants
///         (<c>IsValidMount20</c>), and the standalone Puma1/Puma2 and Christmas-mount ids were NOT transcribed
///         into the behavior contract -- only their tier grouping and count were. Per this codebase's hard rule
///         against inventing item ids, they are absent here rather than guessed. Consequently
///         <see cref="IsRecognizedMount" /> is a PARTIAL recognizer: a true result is authoritative, but a false
///         result does NOT prove an id is not a mount. Do not use this as a rejection gate until a follow-up
///         contract supplies the tier member id table. (The only concrete mount id visible elsewhere in this
///         codebase is <c>ANIMAL_NUM_TIGER1</c> = 1301, per <c>game.Characters.MountItemId</c>'s own comment; it
///         is not added here because its tier membership and the seven other tier-1 ids around it are exactly
///         what the missing table would supply.)
///     </para>
/// </remarks>
public static class MountCatalog
{
    /// <summary>Inclusive bounds of the <c>GIFT_EVENT</c> gift-event mount range (live in production).</summary>
    public const int GiftEventMinId = 8301;

    /// <summary>Inclusive bounds of the <c>GIFT_EVENT</c> gift-event mount range (live in production).</summary>
    public const int GiftEventMaxId = 8331;

    /// <summary>The standalone mount ids the aggregate accepts by exact match (contract-transcribed subset).</summary>
    private static readonly FrozenSet<int> StandaloneIds = new[] { 559 }.ToFrozenSet();

    /// <summary>True for the gift-event mount range 8301-8331 (<c>GIFT_EVENT</c> defined -> live in production).</summary>
    public static bool IsGiftEventMount(int itemId)
    {
        return itemId is >= GiftEventMinId and <= GiftEventMaxId;
    }

    /// <summary>
    ///     True for the mount-id ranges the behavior contract transcribed verbatim (single id 559, 1332-1341,
    ///     gift-event 8301-8331, 19002-19011). A true result is authoritative; a false result is NOT -- see this
    ///     type's own GAP remarks for the tier member ids not yet supplied.
    /// </summary>
    public static bool IsRecognizedMount(int itemId)
    {
        return StandaloneIds.Contains(itemId)
               || itemId is >= 1332 and <= 1341
               || IsGiftEventMount(itemId)
               || itemId is >= 19002 and <= 19011;
    }
}
