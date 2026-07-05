using Fenrir.Application.Game.World;
using Fenrir.Application.Game.World.ZoneWar;

namespace Fenrir.Application.Game.Handlers.Tribes.Services;

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
    ValueTask<TribeVoteActionResult> RegisterCandidacyAsync(PlayerRuntimeState player, byte slotIndex,
        CancellationToken ct);

    ValueTask<TribeVoteActionResult> CastVoteAsync(PlayerRuntimeState player, byte slotIndex, CancellationToken ct);
}

/// <summary>
///     See <see cref="ITribeVoteService" />. Sort 1 (candidacy, TRIBE_VOTE_V2 branch, S04_MyWork02.cpp:11610+)
///     requires <see cref="TribeVoteElection.Phase" /> to be <see cref="TribeVotePhase.Candidacy" />; sort 3
///     (vote, same file's case 3) requires <see cref="TribeVotePhase.Voting" />.
/// </summary>
public sealed class TribeVoteService(TribeVoteElection election) : ITribeVoteService
{
    public async ValueTask<TribeVoteActionResult> RegisterCandidacyAsync(PlayerRuntimeState player, byte slotIndex,
        CancellationToken ct)
    {
        var outcome = await election.TryRegisterCandidacyAsync(player, slotIndex, ct);
        return outcome == TribeVoteCandidacyOutcome.Registered
            ? new TribeVoteActionResult(TribeVoteAction.Accept, 0)
            : TribeVoteActionResult.Aborted;
    }

    public async ValueTask<TribeVoteActionResult> CastVoteAsync(PlayerRuntimeState player, byte slotIndex,
        CancellationToken ct)
    {
        var voteOutcome = await election.TryCastVoteAsync(player, slotIndex, ct);
        return voteOutcome switch
        {
            TribeVoteCastOutcome.Cast => new TribeVoteActionResult(TribeVoteAction.Accept, 0),
            TribeVoteCastOutcome.AlreadyVotedThisWindow => new TribeVoteActionResult(TribeVoteAction.RejectNoAbort, 1),
            _ => TribeVoteActionResult.Aborted
        };
    }
}
