using Fenrir.Application.Game.Domain.World;

namespace Fenrir.Application.Game.Abstractions.FishingConsumables;

public interface IFishingProgressService
{
    public ValueTask<FishingProgressResult?> PollBiteAsync(Zone zone, PlayerRuntimeState state, int characterId,
        CancellationToken cancellationToken);

    public ValueTask<FishingProgressResult?> RecastAsync(Zone zone, PlayerRuntimeState state, int characterId,
        CancellationToken cancellationToken);

    public ValueTask<FishingProgressResult?> ForceStepAsync(Zone zone, PlayerRuntimeState state, int characterId,
        int step, CancellationToken cancellationToken);
}

public sealed record FishingProgressResult(int ResultSort, int FishingState, int FishingStep, bool BroadcastCapture);
