using Fenrir.Application.Game.Domain.World;

namespace Fenrir.Application.Game.Abstractions.FishingConsumables;

/// <summary>
///     Business logic for <c>FishingProgressHandler</c> (CZ_FISHING_RESULT_SEND, opcode 104): Sort=1 polls for a
///     bite (silent no-op unless the step/cast-clock gate passes), Sort=2 recasts, Sort=3 is a client-forced step.
/// </summary>
public interface IFishingProgressService
{
    /// <summary>Null means the legacy's silent no-op: no reply, no zone mirror at all.</summary>
    public FishingProgressResult? PollBite(Zone zone, PlayerRuntimeState state, int characterId);

    public FishingProgressResult Recast(Zone zone, PlayerRuntimeState state, int characterId);

    public FishingProgressResult ForceStep(Zone zone, PlayerRuntimeState state, int characterId, int step);
}

public sealed record FishingProgressResult(int ResultSort, int FishingState, int FishingStep);
