using Fenrir.Application.Game.Domain.Inventory;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Network.Serialization.Packets.Zone;

namespace Fenrir.Application.Game.Abstractions.ItemModification;

public enum RuneSocketOutcome
{
    Rejected,
    InventoryFull,
    Applied
}

public readonly record struct RuneInsertResult(RuneSocketOutcome Outcome);

/// <summary>
///     <see cref="GrantedItem" /> is the withdrawn rune's resulting inventory stack (populated only when
///     <see cref="Outcome" /> is <see cref="RuneSocketOutcome.Applied" />) -- <c>RuneSocketHandler</c> sends it
///     back as a <c>ZC_ADD_USER_INVENTORY_ITEM_RECV</c> before the <c>RuneSocketResponse</c> itself, mirroring
///     <c>CraftItemHandler</c>'s ordering for the same "client learns of the new item before the result packet
///     referencing it" reason.
/// </summary>
public readonly record struct RuneRemoveResult(
    RuneSocketOutcome Outcome,
    byte Page,
    byte Index,
    int ItemIndex,
    ItemStack? GrantedItem = null);

public interface IRuneSocketService
{
    public ValueTask<RuneInsertResult> InsertAsync(RuneSocketRequest packet, Zone zone, PlayerRuntimeState state,
        int characterId, CancellationToken cancellationToken);

    public ValueTask<RuneRemoveResult> RemoveAsync(RuneSocketRequest packet, Zone zone, PlayerRuntimeState state,
        int characterId, CancellationToken cancellationToken);
}
