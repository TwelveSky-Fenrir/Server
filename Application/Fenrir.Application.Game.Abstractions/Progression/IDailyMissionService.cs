using Fenrir.Application.Game.Domain.World;

namespace Fenrir.Application.Game.Abstractions.Progression;

public enum DailyMissionClaimOutcome
{
    Aborted,

    InventoryFull,

    Success
}

public readonly record struct DailyMissionClaimResult(
    DailyMissionClaimOutcome Outcome,
    int JoinWar,
    int KillOtherTribe);

public interface IDailyMissionService
{
    public ValueTask<DailyMissionClaimResult> ClaimAsync(int characterId, Zone zone, PlayerRuntimeState state,
        CancellationToken cancellationToken);
}
