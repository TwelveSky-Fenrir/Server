using Fenrir.Application.Game.Abstractions.Tribes;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Application.Game.Domain.World.ZoneWar;

namespace Fenrir.Application.Game.Services.Tribes;

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
