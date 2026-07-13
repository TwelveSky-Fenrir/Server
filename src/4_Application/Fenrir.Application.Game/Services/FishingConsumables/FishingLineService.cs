using Fenrir.Application.Game.Abstractions.FishingConsumables;
using Fenrir.Application.Game.Domain.Fishing;
using Fenrir.Application.Game.Domain.World;

namespace Fenrir.Application.Game.Services.FishingConsumables;

public sealed class FishingLineService : IFishingLineService
{
    public FishingLineResult Cast(Zone zone, PlayerRuntimeState state, int characterId)
    {
        if (!FishingCastResolver.HasWaterAtCurrentPosition(zone.Geometry, state.PosX, state.PosY, state.PosZ))
        {
            zone.PostFishingCommand(new FishingZoneCommand(characterId, state.FishingState, state.FishingStep,
                false, false, null));
            return new FishingLineResult(0, 0, 0);
        }

        zone.PostFishingCommand(new FishingZoneCommand(characterId, 1, 2, false, true, 92, DateTime.UtcNow));
        return new FishingLineResult(1, 1, 2);
    }

    public FishingLineResult Reel(Zone zone, PlayerRuntimeState state, int characterId)
    {
        if (state.FishingState == 0)
        {
            zone.PostFishingCommand(new FishingZoneCommand(characterId, state.FishingState, state.FishingStep,
                false, false, null));
            return new FishingLineResult(0, 0, 0);
        }

        zone.PostFishingCommand(new FishingZoneCommand(characterId, 0, 0, false, false, null));
        return new FishingLineResult(2, 0, 0);
    }
}
