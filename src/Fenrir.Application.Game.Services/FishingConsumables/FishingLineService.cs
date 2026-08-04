using Fenrir.Application.Game.Abstractions.FishingConsumables;
using Fenrir.Application.Game.Domain.Fishing;
using Fenrir.Application.Game.Domain.Simulation;
using Fenrir.Application.Game.Domain.World;

namespace Fenrir.Application.Game.Services.FishingConsumables;

public sealed class FishingLineService : IFishingLineService
{
    public async ValueTask<FishingLineResult?> CastAsync(Zone zone, PlayerRuntimeState state, int characterId,
        CancellationToken cancellationToken)
    {
        if (!FishingCastResolver.HasWaterAtCurrentPosition(zone.Geometry, state.PosX, state.PosY, state.PosZ))
        {
            if ((await zone.PostFishingCommandAndWaitForResultAsync(
                    new FishingZoneCommand(characterId, state.FishingState, state.FishingStep, false, false, null),
                    cancellationToken)).Kind != ZoneCommandResultKind.Applied)
                return null;

            return new FishingLineResult(0, 0, 0);
        }

        if ((await zone.PostFishingCommandAndWaitForResultAsync(
                new FishingZoneCommand(characterId, 1, 2, false, false, null, DateTime.UtcNow), cancellationToken))
            .Kind !=
            ZoneCommandResultKind.Applied)
            return null;

        return new FishingLineResult(1, 1, 2);
    }

    public async ValueTask<FishingLineResult?> ReelAsync(Zone zone, PlayerRuntimeState state, int characterId,
        CancellationToken cancellationToken)
    {
        if (state.FishingState == 0)
        {
            if ((await zone.PostFishingCommandAndWaitForResultAsync(
                    new FishingZoneCommand(characterId, state.FishingState, state.FishingStep, false, false, null),
                    cancellationToken)).Kind != ZoneCommandResultKind.Applied)
                return null;

            return new FishingLineResult(0, 0, 0);
        }

        if ((await zone.PostFishingCommandAndWaitForResultAsync(
                new FishingZoneCommand(characterId, 0, 0, false, false, null), cancellationToken)).Kind !=
            ZoneCommandResultKind.Applied)
            return null;

        return new FishingLineResult(2, 0, 0);
    }
}
