using Fenrir.Application.Game.Domain.World;

namespace Fenrir.Application.Game.Abstractions.BuffsMountsCosmetics;

public interface IMountStateService
{
    public ValueTask<MountStateResult> ApplyAsync(Zone zone, PlayerRuntimeState state, int characterId,
        int accountId, int sort, int value, CancellationToken cancellationToken);
}

public enum MountStateOutcome
{
    NoReply,
    Disconnect,
    Select,
    Deselect,
    Mount,
    Dismount,
    DeleteMount,
    DeleteAttribute
}

public readonly record struct MountStateResult(MountStateOutcome Outcome);
