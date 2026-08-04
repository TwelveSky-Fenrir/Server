using Fenrir.Application.Game.Abstractions.FishingConsumables;
using Fenrir.Application.Game.Domain.Combat;
using Fenrir.Application.Game.Domain.Fishing;
using Fenrir.Application.Game.Domain.Simulation;
using Fenrir.Application.Game.Domain.World;

namespace Fenrir.Application.Game.Services.FishingConsumables;

public sealed class FishingProgressService : IFishingProgressService
{
    public async ValueTask<FishingProgressResult?> PollBiteAsync(Zone zone, PlayerRuntimeState state, int characterId,
        CancellationToken cancellationToken)
    {
        if (state.FishingState == 0 || state.FishingStep != 3 || state.FishingCastAtUtc is not { } castAt ||
            DateTime.UtcNow - castAt < TimeSpan.FromMinutes(1))
            return null;

        var hit = FishingRewardResolver.RollBite(SystemRandomSource.Instance);
        return await ReplyAsync(zone, state, characterId, 1, hit ? 4 : 5, null, true, hit, cancellationToken);
    }

    public ValueTask<FishingProgressResult?> RecastAsync(Zone zone, PlayerRuntimeState state, int characterId,
        CancellationToken cancellationToken)
    {
        if (state.FishingState == 0)
            return ReplyAsync(zone, state, characterId, 2, state.FishingStep, null, false,
                state.FishingBiteWasHit, cancellationToken);

        return ReplyAsync(zone, state, characterId, 2, 2, DateTime.UtcNow, false, state.FishingBiteWasHit,
            cancellationToken);
    }

    public ValueTask<FishingProgressResult?> ForceStepAsync(Zone zone, PlayerRuntimeState state, int characterId,
        int step, CancellationToken cancellationToken)
    {
        var captured = step is 4 or 5;
        return ReplyAsync(zone, state, characterId, 3, step, null, captured, step == 4, cancellationToken);
    }

    private static async ValueTask<FishingProgressResult?> ReplyAsync(Zone zone, PlayerRuntimeState state,
        int characterId, int resultSort, int newStep, DateTime? castAt, bool armCatch, bool biteWasHit,
        CancellationToken cancellationToken)
    {
        var caught = newStep is 4 or 5;
        if ((await zone.PostFishingCommandAndWaitForResultAsync(
                new FishingZoneCommand(characterId, state.FishingState, newStep,
                    state.CatchingFish || armCatch, false, null, castAt, BiteWasHit: biteWasHit), cancellationToken)).Kind !=
            ZoneCommandResultKind.Applied)
            return null;

        return new FishingProgressResult(resultSort, state.FishingState, newStep, caught);
    }
}
