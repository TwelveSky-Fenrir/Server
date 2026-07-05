using Fenrir.Application.Game.Domain.World;

namespace Fenrir.Application.Game.Abstractions.BuffsMountsCosmetics;

/// <summary>Business logic behind <see cref="MountStateHandler" /> (CZ_ANIMAL_STATE_SEND, op87).</summary>
public interface IMountStateService
{
    public MountStateResult Apply(Zone zone, PlayerRuntimeState state, int characterId, int sort, int value);
}

public enum MountStateOutcome
{
    NoReply,
    Disconnect,
    Select,
    Deselect,
    Mount,
    Dismount
}

public readonly record struct MountStateResult(MountStateOutcome Outcome);
