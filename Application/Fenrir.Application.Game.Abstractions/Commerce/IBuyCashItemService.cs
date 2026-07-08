using Fenrir.Application.Game.Domain.World;
using Fenrir.Network.Serialization.Zone.Packets.Zone;

namespace Fenrir.Application.Game.Abstractions.Commerce;

/// <summary>Business logic for CZ_BUY_CASH_ITEM_SEND (opcode 42), extracted from <see cref="BuyCashItemHandler" />.</summary>
public interface IBuyCashItemService
{
    /// <summary>
    ///     Resolves and applies a cash-shop purchase. Returns <c>null</c> when the caller should abort the
    ///     session as faulted; otherwise the response to send back to the client.
    /// </summary>
    public ValueTask<BuyCashItemResponse?> ResolveAndApplyAsync(BuyCashItemRequest packet, Zone zone,
        PlayerRuntimeState state, int characterId, int accountId, CancellationToken cancellationToken);
}
