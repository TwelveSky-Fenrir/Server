using Fenrir.Application.Game.Domain.World;

namespace Fenrir.Application.Game.Abstractions.BuffsMountsCosmetics;

public interface ICostumeStateService
{
    public ValueTask<CostumeStateResult> ApplyAsync(Zone zone, PlayerRuntimeState state, int characterId,
        int accountId, int sort, int value, CancellationToken cancellationToken);
}

public enum CostumeStateOutcome
{
    NoReply,
    Disconnect,
    Reply
}

public readonly record struct CostumeStateResult(
    CostumeStateOutcome Outcome,
    int ResultCode = 0,
    int Page = -1,
    int PosX = -1,
    int PosY = -1,
    int ItemIndex = -1,
    int CostumeDate = 0);
