using Fenrir.Application.Game.Combat;
using Fenrir.Application.Game.Fishing;
using Fenrir.Application.Game.World;
using Fenrir.Network.Serialization.Packets.Zone;

namespace Fenrir.Application.Game.Handlers.FishingConsumables.Services;

/// <summary>
///     Business logic for <c>FishingProgressHandler</c> (CZ_FISHING_RESULT_SEND, opcode 104): Sort=1 polls for a
///     bite (silent no-op unless the step/cast-clock gate passes), Sort=2 recasts, Sort=3 is a client-forced step.
/// </summary>
public interface IFishingProgressService
{
    /// <summary>Null means the legacy's silent no-op: no reply, no zone mirror at all.</summary>
    FishingProgressResult? PollBite(Zone zone, PlayerRuntimeState state, int characterId);

    FishingProgressResult Recast(Zone zone, PlayerRuntimeState state, int characterId);

    FishingProgressResult ForceStep(Zone zone, PlayerRuntimeState state, int characterId, int step);
}

public sealed class FishingProgressService : IFishingProgressService
{
    public FishingProgressResult? PollBite(Zone zone, PlayerRuntimeState state, int characterId)
    {
        if (state.FishingState == 0 || state.FishingStep != 3 || state.FishingCastAtUtc is not { } castAt ||
            DateTime.UtcNow - castAt < TimeSpan.FromMinutes(1))
            return null;

        var step = FishingRewardResolver.RollBite(SystemRandomSource.Instance) ? 4 : 5;
        return Reply(zone, state, characterId, 1, step, null);
    }

    public FishingProgressResult Recast(Zone zone, PlayerRuntimeState state, int characterId)
    {
        if (state.FishingState == 0)
            return Reply(zone, state, characterId, 2, state.FishingStep, null);

        return Reply(zone, state, characterId, 2, 2, DateTime.UtcNow);
    }

    public FishingProgressResult ForceStep(Zone zone, PlayerRuntimeState state, int characterId, int step)
    {
        return Reply(zone, state, characterId, 3, step, null);
    }

    private static FishingProgressResult Reply(Zone zone, PlayerRuntimeState state, int characterId,
        int resultSort, int newStep, DateTime? castAt)
    {
        var caught = newStep is 4 or 5;
        zone.PostFishingCommand(new FishingZoneCommand(characterId, state.FishingState, newStep,
            state.CatchingFish || caught, caught, caught ? 93 : null, castAt));

        return new FishingProgressResult(resultSort, state.FishingState, newStep);
    }
}

public sealed record FishingProgressResult(int ResultSort, int FishingState, int FishingStep);
