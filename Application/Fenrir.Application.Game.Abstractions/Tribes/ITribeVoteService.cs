using Fenrir.Application.Game.Domain.World;

namespace Fenrir.Application.Game.Abstractions.Tribes;

public enum TribeVoteAction
{
    Accept,

        RejectNoAbort,

    Abort
}

public readonly record struct TribeVoteActionResult(TribeVoteAction Action, int Result)
{
    public static readonly TribeVoteActionResult Aborted = new(TribeVoteAction.Abort, 0);
}

public interface ITribeVoteService
{
    public ValueTask<TribeVoteActionResult> RegisterCandidacyAsync(PlayerRuntimeState player, byte slotIndex,
        CancellationToken ct);

    public ValueTask<TribeVoteActionResult> CastVoteAsync(PlayerRuntimeState player, byte slotIndex,
        CancellationToken ct);
}
