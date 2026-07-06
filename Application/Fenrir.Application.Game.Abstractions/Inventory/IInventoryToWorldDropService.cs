using Fenrir.Application.Game.Domain.World;
using Fenrir.Application.Game.Domain.World.Loot;
using Fenrir.Network.Serialization.Packets.Shared;

namespace Fenrir.Application.Game.Abstractions.Inventory;

/// <summary>
///     Outcome discriminator for <see cref="IInventoryToWorldDropService.DropToWorldAsync" /> -- mirrors the
///     3-way convention <see cref="Fenrir.Application.Game.Abstractions.GenericAction.GenericActionStatus" />
///     already uses for the rest of the tSort 209 sibling family: <c>Aborted</c> means disconnect with no
///     acknowledgment at all, <c>Failed</c> means the standard acknowledgment with a generic non-zero code,
///     <c>Succeeded</c> means the standard acknowledgment with code zero.
/// </summary>
public enum InventoryToWorldDropStatus
{
    Aborted,
    Failed,
    Succeeded
}

/// <summary>
///     Result of <see cref="IInventoryToWorldDropService.DropToWorldAsync" />. <see cref="Spawn" /> is populated
///     only on <see cref="InventoryToWorldDropStatus.Succeeded" /> -- see that method's own remarks for why
///     actually turning it into a visible, broadcast <see cref="GroundItemEntity" /> is explicitly NOT this
///     service's job yet.
/// </summary>
public readonly record struct InventoryToWorldDropResult(
    InventoryToWorldDropStatus Status,
    GroundItemSpawnPlan? Spawn = null)
{
    public static readonly InventoryToWorldDropResult Aborted = new(InventoryToWorldDropStatus.Aborted);
    public static readonly InventoryToWorldDropResult Failed = new(InventoryToWorldDropStatus.Failed);
}

/// <summary>
///     Business logic behind <c>CZ_PROCESS_DATA_SEND</c>'s tSort 209 ("drop item from inventory to the ground
///     at the player's own location") -- see <see cref="Fenrir.Application.Game.Domain.Inventory.InventoryToWorldDropPolicy" />
///     for the pure rules this orchestrates. Deliberately NOT wired into <c>GenericActionHandler</c>'s existing
///     tSort dispatch switch yet -- that integration step (including resolving this method's own
///     <paramref name="premiumPageAccessAllowed" />-equivalent input, and routing a successful
///     <see cref="GroundItemSpawnPlan" /> into an actual, capacity/value/socket-aware <c>Zone</c> spawn call)
///     is left to a follow-up task, not this one.
/// </summary>
public interface IInventoryToWorldDropService
{
    /// <param name="zone">The dropping character's current zone.</param>
    /// <param name="state">The dropping character's own runtime state.</param>
    /// <param name="characterId">The dropping character's id.</param>
    /// <param name="accountId">The dropping character's owning account id, for the unique-item drop audit log row.</param>
    /// <param name="move">
    ///     The shared 28-byte generic payload (<c>DefaultPData</c>) -- only <c>Page1</c>/<c>Index1</c>/<c>Quantity1</c>
    ///     are consulted; <c>Page2</c>/<c>Index2</c>/<c>XPost2</c>/<c>YPost2</c> are reused-envelope noise for
    ///     this particular sub-operation and are ignored here, matching the behavior contract.
    /// </param>
    /// <param name="premiumPageAccessAllowed">
    ///     Caller-supplied: whether the account's second/premium inventory page has not yet expired. No such
    ///     field exists on <c>PlayerRuntimeState</c> today (same gap <c>StoreItemTransferPolicy</c>/
    ///     <c>SaveBankItemTransferPolicy</c> already document for their own equivalent gates) -- only consulted
    ///     when <c>move.Page1</c> is the second page.
    /// </param>
    public ValueTask<InventoryToWorldDropResult> DropToWorldAsync(Zone zone, PlayerRuntimeState state,
        int characterId, int accountId, DefaultPData move, bool premiumPageAccessAllowed,
        CancellationToken cancellationToken);
}
