using Fenrir.Application.Game.Domain.Crafting;
using Fenrir.Application.Game.Domain.World;

namespace Fenrir.Application.Game.Abstractions.ItemModification;

/// <summary>
///     Business logic for CZ_PROCESS_DATA_SEND tSort 3000 -- rune-stone stat crafting on equipment. Not yet
///     reachable from <c>GenericActionHandler</c>'s dispatch switch; wiring it up (reading the 7 generic
///     <c>DefaultPData</c> fields, calling <see cref="CraftAsync" />, and sending the
///     <c>GenericActionResponse</c> reply with <c>Result</c>/<c>RuneValue</c> set from the returned
///     <see cref="RuneStoneCraftResult" />) is a separate integration step -- same "declared ahead of its own
///     dispatch wiring" posture as <see cref="IGenericActionService.AllocateStatPointAsync" />.
/// </summary>
public interface IRuneStoneCraftService
{
    /// <param name="sourcePage">Wire tPage1 -- 0/1, which inventory page holds the rune-stone source item.</param>
    /// <param name="sourceSlot">Wire tIndex1 -- 0-63, the source item's slot within <paramref name="sourcePage" />.</param>
    /// <param name="destinationPage">Wire tPage2 -- 0/1, which inventory page holds the destination rune-core item.</param>
    /// <param name="destinationSlot">
    ///     Wire tIndex2 -- 0-63, the destination item's slot within
    ///     <paramref name="destinationPage" />.
    /// </param>
    /// <param name="statSlotSelector">Wire tXPost2, repurposed as the 100/200/300/400 stat-slot selector.</param>
    /// <param name="destinationPackedStat">
    ///     The destination item's CURRENT packed STR/DEX/VIT/INT value (see <see cref="RuneStoneStatCodec" />,
    ///     whose own remarks explain why this is caller-supplied rather than read off <c>ItemStack</c>). See
    ///     <c>Fenrir.Application.Game.Services.ItemModification.RuneStoneCraftService</c>'s remarks for the
    ///     open question of where this value physically lives once persistence is wired up.
    /// </param>
    /// <param name="secondInventoryPageAccessible">
    ///     Whether the character's second-inventory-page access has not expired -- caller-supplied because
    ///     Fenrir has no <c>wInventoryDate</c>-equivalent field yet (same pre-existing gap as
    ///     <c>SaveBankItemTransferPolicy</c> et al.).
    /// </param>
    public ValueTask<RuneStoneCraftResult> CraftAsync(
        int sourcePage, int sourceSlot,
        int destinationPage, int destinationSlot,
        int statSlotSelector, int destinationPackedStat,
        bool secondInventoryPageAccessible,
        Zone zone, PlayerRuntimeState state, int characterId,
        CancellationToken cancellationToken);
}
