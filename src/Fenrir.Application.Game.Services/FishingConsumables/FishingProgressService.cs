using Fenrir.Application.Game.Abstractions.FishingConsumables;
using Fenrir.Application.Game.Domain.Combat;
using Fenrir.Application.Game.Domain.Fishing;
using Fenrir.Application.Game.Domain.World;

namespace Fenrir.Application.Game.Services.FishingConsumables;

public sealed class FishingProgressService : IFishingProgressService
{
    public FishingProgressResult? PollBite(Zone zone, PlayerRuntimeState state, int characterId)
    {
        if (state.FishingState == 0 || state.FishingStep != 3 || state.FishingCastAtUtc is not { } castAt ||
            DateTime.UtcNow - castAt < TimeSpan.FromMinutes(1))
            return null;

        var hit = FishingRewardResolver.RollBite(SystemRandomSource.Instance);
        return Reply(zone, state, characterId, 1, hit ? 4 : 5, null, true, hit);
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
        int resultSort, int newStep, DateTime? castAt, bool armCatch = false, bool biteWasHit = false)
    {
        var caught = newStep is 4 or 5;
        zone.PostFishingCommand(new FishingZoneCommand(characterId, state.FishingState, newStep,
            state.CatchingFish || armCatch, caught, caught ? 93 : null, castAt, BiteWasHit: biteWasHit));

        return new FishingProgressResult(resultSort, state.FishingState, newStep);
    }
}
