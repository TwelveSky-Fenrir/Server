using Fenrir.Application.Game.Domain.World;

namespace Fenrir.Application.Game.Abstractions.FishingConsumables;

public interface IFishingProgressService
{

        public FishingProgressResult? PollBite(Zone zone, PlayerRuntimeState state, int characterId);

    public FishingProgressResult Recast(Zone zone, PlayerRuntimeState state, int characterId);

    public FishingProgressResult ForceStep(Zone zone, PlayerRuntimeState state, int characterId, int step);
}

public sealed record FishingProgressResult(int ResultSort, int FishingState, int FishingStep);
