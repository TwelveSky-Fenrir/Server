using Fenrir.Application.Game.Domain.World;
using Fenrir.Network.Serialization.Packets.Zone;

namespace Fenrir.Application.Game.Abstractions.ItemModification;

public enum DestroyItemOutcome
{
    Rejected,
    Applied
}

public readonly record struct DestroyItemResult(
    DestroyItemOutcome Outcome,
    int Money,
    int StoneItemId,
    int Quantity,
    int Serial);

public interface IDestroyItemService
{
    public ValueTask<DestroyItemResult> DestroyAsync(DestroyItemRequest packet, Zone zone, PlayerRuntimeState state,
        int characterId, int accountId, CancellationToken cancellationToken);
}
