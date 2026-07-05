using Fenrir.Application.Game.Mounts;
using Fenrir.Application.Game.World;

namespace Fenrir.Application.Game.Handlers.BuffsMountsCosmetics.Services;

/// <summary>Business logic behind <see cref="MountStateHandler" /> (CZ_ANIMAL_STATE_SEND, op87).</summary>
public interface IMountStateService
{
    MountStateResult Apply(Zone zone, PlayerRuntimeState state, int characterId, int sort, int value);
}

public sealed class MountStateService : IMountStateService
{
    public MountStateResult Apply(Zone zone, PlayerRuntimeState state, int characterId, int sort, int value)
    {
        var context = new MountStateResolver.Context(state.AnimalIndex, state.AnimalTime, state.ActionSort,
            state.MountGarage);
        var result = MountStateResolver.Resolve(sort, value, in context);

        switch (result.Kind)
        {
            case MountStateResolver.ResultKind.NoReply:
                return new MountStateResult(MountStateOutcome.NoReply);

            case MountStateResolver.ResultKind.Disconnect:
                return new MountStateResult(MountStateOutcome.Disconnect);

            case MountStateResolver.ResultKind.Select:
                zone.PostMountCommand(new MountZoneCommand(characterId, result.NewAnimalIndex));
                return new MountStateResult(MountStateOutcome.Select);

            case MountStateResolver.ResultKind.Deselect:
                zone.PostMountCommand(new MountZoneCommand(characterId, result.NewAnimalIndex));
                return new MountStateResult(MountStateOutcome.Deselect);

            case MountStateResolver.ResultKind.Mount:
            {
                var maxLife = state.Stats?.MaxLife ?? state.MaxLife;
                var maxMana = state.Stats?.MaxMana ?? state.MaxMana;
                zone.PostMountCommand(new MountZoneCommand(characterId, result.NewAnimalIndex,
                    result.NewAnimalNumber, 0, maxLife, maxMana,
                    Broadcast: MountBroadcastKind.Mount));
                return new MountStateResult(MountStateOutcome.Mount);
            }

            case MountStateResolver.ResultKind.Dismount:
            {
                var maxLife = state.Stats?.MaxLife ?? state.MaxLife;
                var maxMana = state.Stats?.MaxMana ?? state.MaxMana;
                zone.PostMountCommand(new MountZoneCommand(characterId, result.NewAnimalIndex,
                    0, 0, maxLife, maxMana,
                    Broadcast: MountBroadcastKind.Dismount));
                return new MountStateResult(MountStateOutcome.Dismount);
            }

            default:
                return new MountStateResult(MountStateOutcome.NoReply);
        }
    }
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
