using Fenrir.Application.Game.Domain.World;

namespace Fenrir.Application.Game.Abstractions.FishingConsumables;

public interface IFishingCatchService
{
    public ValueTask ResolveAndApplyAsync(Zone zone, PlayerRuntimeState state, int characterId, IPacketSession session,
        CancellationToken cancellationToken);
}
