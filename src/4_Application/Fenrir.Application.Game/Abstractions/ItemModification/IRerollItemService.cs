using Fenrir.Application.Game.Domain.World;
using Fenrir.Application.Game.Packets.Zone;

namespace Fenrir.Application.Game.Abstractions.ItemModification;

public enum RerollItemOutcome
{
    Rejected,
    NoCandidate,
    Applied
}

public readonly record struct RerollItemResult(RerollItemOutcome Outcome, int Cost, int[] Value);

public interface IRerollItemService
{
    public ValueTask<RerollItemResult> RerollAsync(RerollItemRequest packet, Zone zone, PlayerRuntimeState state,
        int characterId, CancellationToken cancellationToken);
}
