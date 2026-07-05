using Fenrir.Application.Game.Fishing;
using Fenrir.Application.Game.World;
using Fenrir.Contracts.Packets.Zone;

namespace Fenrir.Application.Game.Handlers.FishingConsumables.Services;

/// <summary>
///     Business logic for <c>FishingLineHandler</c> (CZ_FISHING_STATE_SEND, opcode 103): Sort=1 casts (gated by a
///     mesh check under the caster's own position), Sort=2 reels in.
/// </summary>
public interface IFishingLineService
{
    FishingLineResult Cast(Zone zone, PlayerRuntimeState state, int characterId);

    FishingLineResult Reel(Zone zone, PlayerRuntimeState state, int characterId);
}

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

public sealed record FishingLineResult(int Result, int FishingState, int FishingStep);
