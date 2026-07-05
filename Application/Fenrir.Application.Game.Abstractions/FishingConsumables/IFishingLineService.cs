using Fenrir.Application.Game.Domain.World;

namespace Fenrir.Application.Game.Abstractions.FishingConsumables;

/// <summary>
///     Business logic for <c>FishingLineHandler</c> (CZ_FISHING_STATE_SEND, opcode 103): Sort=1 casts (gated by a
///     mesh check under the caster's own position), Sort=2 reels in.
/// </summary>
public interface IFishingLineService
{
    public FishingLineResult Cast(Zone zone, PlayerRuntimeState state, int characterId);

    public FishingLineResult Reel(Zone zone, PlayerRuntimeState state, int characterId);
}

public sealed record FishingLineResult(int Result, int FishingState, int FishingStep);
