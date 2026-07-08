using Fenrir.Application.Game.Domain.World;
using Fenrir.Network.Serialization.Zone.Packets.Zone;

namespace Fenrir.Application.Game.Abstractions.Commerce;

/// <summary>Business logic for CZ_BUY_BLOOD_MARK_SEND (opcode 141), extracted from <see cref="BuyBloodMarkItemHandler" />.</summary>
public interface IBuyBloodMarkItemService
{
    /// <summary>
    ///     Resolves and applies a blood-mark purchase. Returns <c>null</c> when the caller should abort the
    ///     session as faulted; otherwise the response to send back to the client.
    /// </summary>
    public ValueTask<BuyBloodMarkItemResponse?> ResolveAndApplyAsync(BuyBloodMarkItemRequest packet, Zone zone,
        PlayerRuntimeState state, int characterId, CancellationToken cancellationToken);
}
