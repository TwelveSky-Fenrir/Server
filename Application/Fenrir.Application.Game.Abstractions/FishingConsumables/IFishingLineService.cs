using Fenrir.Application.Game.Domain.World;

namespace Fenrir.Application.Game.Abstractions.FishingConsumables;

public interface IFishingLineService
{
    public FishingLineResult Cast(Zone zone, PlayerRuntimeState state, int characterId);

    public FishingLineResult Reel(Zone zone, PlayerRuntimeState state, int characterId);
}

public sealed record FishingLineResult(int Result, int FishingState, int FishingStep);
