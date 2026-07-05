using Fenrir.Application.Game.Domain.World;

namespace Fenrir.Application.Game.Abstractions.Progression;

public enum DailyMissionClaimOutcome
{
    /// <summary>A pre-claim gate failed (level/war/kill-tribe thresholds, or an unresolvable roll) -- caller must disconnect.</summary>
    Aborted,

    /// <summary>Every gate passed but no empty inventory slot was found for the reward -- clean failure (Result = 3).</summary>
    InventoryFull,

    Success
}

/// <summary>
///     <see cref="DailyMissionClaimOutcome.Success" /> carries the post-claim <see cref="JoinWar" />/
///     <see cref="KillOtherTribe" /> counters (both decremented by their claim requirement); unused for the other
///     outcomes.
/// </summary>
public readonly record struct DailyMissionClaimResult(
    DailyMissionClaimOutcome Outcome,
    int JoinWar,
    int KillOtherTribe);

public interface IDailyMissionService
{
    public ValueTask<DailyMissionClaimResult> ClaimAsync(int characterId, Zone zone, PlayerRuntimeState state,
        CancellationToken cancellationToken);
}
