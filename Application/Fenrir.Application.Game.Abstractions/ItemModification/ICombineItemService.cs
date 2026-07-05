using Fenrir.Application.Game.Domain.World;
using Fenrir.Network.Serialization.Packets.Zone;

namespace Fenrir.Application.Game.Abstractions.ItemModification;

public enum CombineItemOutcome
{
    Rejected,
    Applied
}

public readonly record struct CombineItemResult(CombineItemOutcome Outcome, int ResultCode, int Cost);

public interface ICombineItemService
{
    public ValueTask<CombineItemResult> CombineAsync(CombineItemRequest packet, Zone zone, PlayerRuntimeState state,
        int characterId, CancellationToken cancellationToken);
}
