using Fenrir.Application.Game.Domain.World;

namespace Fenrir.Application.Game.Abstractions.FishingConsumables;

public interface IFishingLineService
{
    public ValueTask<FishingLineResult?> CastAsync(Zone zone, PlayerRuntimeState state, int characterId,
        CancellationToken cancellationToken);

    public ValueTask<FishingLineResult?> ReelAsync(Zone zone, PlayerRuntimeState state, int characterId,
        CancellationToken cancellationToken);
}

public sealed record FishingLineResult(int Result, int FishingState, int FishingStep);
