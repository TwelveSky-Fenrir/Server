using Fenrir.Application.Game.Domain.World;

namespace Fenrir.Application.Game.Abstractions.BuffsMountsCosmetics;

public interface IStellarCoreStateService
{
    public ValueTask<StellarCoreStateResult> ApplyAsync(Zone zone, PlayerRuntimeState state, int characterId, int sort,
        int value, CancellationToken cancellationToken);
}

public enum StellarCoreStateOutcome
{
    NoReply,
    Disconnect,
    Reply
}

public readonly record struct StellarCoreStateResult(
    StellarCoreStateOutcome Outcome,
    int ResultCode = 0,
    int Page = -1,
    int PosX = -1,
    int PosY = -1,
    int ItemIndex = -1);
