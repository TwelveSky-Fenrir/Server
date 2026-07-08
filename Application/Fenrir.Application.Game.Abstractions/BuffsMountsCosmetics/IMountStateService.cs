using Fenrir.Application.Game.Domain.World;

namespace Fenrir.Application.Game.Abstractions.BuffsMountsCosmetics;

/// <summary>
///     Business logic behind <see cref="MountStateHandler" /> (CZ_ANIMAL_STATE_SEND, op87). Sort 5 (Delete
///     Mount) and Sort 7 (Delete Rolled Attribute) need real I/O (a CP mirror / an inventory item scan +
///     consumption + durable audit row), so this is asynchronous even though Sort 1-4's own resolution stays
///     purely in-memory.
/// </summary>
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
