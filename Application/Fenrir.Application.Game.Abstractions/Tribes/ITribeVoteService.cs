using Fenrir.Application.Game.Domain.World;

namespace Fenrir.Application.Game.Abstractions.Tribes;

/// <summary>Whether a <see cref="TribeVoteHandler" /> sub-command should abort, echo a rejection, or echo success.</summary>
public enum TribeVoteAction
{
    Accept,

    /// <summary>
    ///     The only rejection that is NOT a disconnect in the legacy (S04_MyWork02.cpp case 3's
    ///     <c>aTribeVoteDate</c> gate replies with Result=1 instead of calling <c>Quit()</c>).
    /// </summary>
    RejectNoAbort,

    Abort
}

/// <summary>Outcome of <see cref="ITribeVoteService" />'s candidacy/vote calls.</summary>
public readonly record struct TribeVoteActionResult(TribeVoteAction Action, int Result)
{
    public static readonly TribeVoteActionResult Aborted = new(TribeVoteAction.Abort, 0);
}

/// <summary>Business logic behind CZ_TRIBE_VOTE_SEND (opcode 83), extracted out of <see cref="TribeVoteHandler" />.</summary>
public interface ITribeVoteService
{
    public ValueTask<TribeVoteActionResult> RegisterCandidacyAsync(PlayerRuntimeState player, byte slotIndex,
        CancellationToken ct);

    public ValueTask<TribeVoteActionResult> CastVoteAsync(PlayerRuntimeState player, byte slotIndex,
        CancellationToken ct);
}
