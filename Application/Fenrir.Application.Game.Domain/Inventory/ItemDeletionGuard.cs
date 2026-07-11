using System.Collections.Frozen;

namespace Fenrir.Application.Game.Domain.Inventory;

/// <summary>
///     <c>CheckDeleteItem</c>'s protected-item-type-identifier guard: refuses to allow removal of any of 15
///     always-protected item type ids from a container, regardless of which build-time macro superficially
///     appears to gate roughly half of them in the legacy source. Both the (unconditional) block containing
///     522/523/524/525/7101/7102/7103/7104 AND the nominally <c>__GOD__</c>-gated block containing
///     886/99011-99016 are live in every real build -- <c>__GOD__</c> is defined in both the branch taken when
///     the build-selector macro is set and the branch taken when it is not, so it is unconditional regardless
///     of that selector's state (independently re-verified by the C21 behavior contract this type implements;
///     see its own Edge cases K). Do not read the build-conditional-looking half of this set as "conditionally
///     protected" -- both halves are always active.
///     <para>
///         Roughly 30 further item ids that were once protected by this same legacy switch now exist only as
///         commented-out case labels and are no longer enforced in any real build -- they are deliberately NOT
///         included in <see cref="ProtectedItemTypeIds" />. Porting a commented-out legacy rule would be
///         inventing behavior, not preserving it.
///     </para>
///     <para>
///         A candidate id outside the underlying switch's own valid range (<see cref="MinValidItemTypeId" />-
///         <see cref="MaxValidItemTypeId" /> inclusive) is ALSO refused -- identical treatment to a protected
///         id, never "unchecked" or "allowed by default."
///     </para>
/// </summary>
/// <remarks>
///     Réf. C++ : Server/Header/function.h:2454-2517 (<c>CheckDeleteItem</c>), specifically :2466-2498 (the
///     ~30 now-commented-out former protected ids, no longer enforced) ; Server/Header/Protocol/DEFINE.h:21-30
///     (the <c>__GOD__</c> macro gating the second block -- independently re-opened and confirmed unconditional
///     in every real build in the C21 contract's own verification pass).
///     <para>
///         Behavior contract: C21 (Commerce/social edges, mini-games &amp; misc guards), section K. Per that
///         contract's own Output/Side-effects sections, which caller(s) actually invoke this immediately before
///         removing an item from a container is explicitly out of scope here ("outside this contract's
///         citations") -- this type models only the pure predicate itself. Candidate integration points in
///         Fenrir today (not wired by this change; a follow-up decision, not re-derived from Server/ here):
///         <c>Fenrir.Application.Game.Services.Inventory.InventoryToWorldDropService</c> (ground-drop) and
///         <c>Fenrir.Application.Game.Services.ItemModification.DestroyItemService</c> (destroy-into-stone) are
///         the two existing Fenrir "remove an item from a container" paths closest in shape to legacy's
///         "any item-deletion code path" trigger, but the contract does not itself cite either as a confirmed
///         legacy call site of <c>CheckDeleteItem</c>, so wiring this guard into either is left unmodeled here
///         rather than guessed at.
///     </para>
/// </remarks>
public static class ItemDeletionGuard
{
    /// <summary>Inclusive lower bound of the underlying legacy switch's own valid candidate-id range.</summary>
    public const int MinValidItemTypeId = 0;

    /// <summary>Inclusive upper bound of the underlying legacy switch's own valid candidate-id range.</summary>
    public const int MaxValidItemTypeId = 99999;

    /// <summary>
    ///     The 15 always-protected item type ids: 522-525 and 7101-7104 (unconditional block) plus 886 and
    ///     99011-99016 (nominally <c>__GOD__</c>-gated, confirmed unconditional in every real build).
    /// </summary>
    public static readonly FrozenSet<int> ProtectedItemTypeIds = new[]
    {
        522, 523, 524, 525,
        7101, 7102, 7103, 7104,
        886,
        99011, 99012, 99013, 99014, 99015, 99016
    }.ToFrozenSet();

    /// <summary>
    ///     True if <paramref name="itemTypeId" /> may be removed from a container; false if it is one of the
    ///     15 always-protected ids in <see cref="ProtectedItemTypeIds" />, or falls outside the underlying
    ///     switch's own <see cref="MinValidItemTypeId" />-<see cref="MaxValidItemTypeId" /> valid range.
    /// </summary>
    public static bool IsDeletionAllowed(int itemTypeId)
    {
        if (itemTypeId is < MinValidItemTypeId or > MaxValidItemTypeId)
            return false;

        return !ProtectedItemTypeIds.Contains(itemTypeId);
    }
}
