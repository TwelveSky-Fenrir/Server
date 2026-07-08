using Fenrir.Application.Game.Abstractions.Tribes;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Application.Game.Domain.World.ZoneWar;
using Microsoft.Extensions.Logging;

namespace Fenrir.Application.Game.Services.Tribes;

/// <summary>
///     See <see cref="ITribeVoteService" />. Sort 1 (candidacy, TRIBE_VOTE_V2 branch, S04_MyWork02.cpp:11610+)
///     requires <see cref="TribeVoteElection.Phase" /> to be <see cref="TribeVotePhase.Candidacy" />; sort 3
///     (vote, same file's case 3) requires <see cref="TribeVotePhase.Voting" />.
/// </summary>
public sealed class TribeVoteService(TribeVoteElection election, ILogger<TribeVoteService> logger)
    : ITribeVoteService
{
    public async ValueTask<TribeVoteActionResult> RegisterCandidacyAsync(PlayerRuntimeState player, byte slotIndex,
        CancellationToken ct)
    {
        var outcome = await election.TryRegisterCandidacyAsync(player, slotIndex, ct);
        if (outcome == TribeVoteCandidacyOutcome.Registered)
        {
            logger.LogInformation(
                "Character {CharacterId} registered Force Leader candidacy for tribe {Tribe} in slot {SlotIndex}",
                player.CharacterId, player.Tribe, slotIndex);
            return new TribeVoteActionResult(TribeVoteAction.Accept, 0);
        }

        logger.LogDebug(
            "Character {CharacterId} Force Leader candidacy registration rejected ({Outcome})",
            player.CharacterId, outcome);
        return TribeVoteActionResult.Aborted;
    }

    public async ValueTask<TribeVoteActionResult> CastVoteAsync(PlayerRuntimeState player, byte slotIndex,
        CancellationToken ct)
    {
        var voteOutcome = await election.TryCastVoteAsync(player, slotIndex, ct);
        switch (voteOutcome)
        {
            case TribeVoteCastOutcome.Cast:
                logger.LogInformation(
                    "Character {CharacterId} cast a Force Leader vote for candidate slot {SlotIndex} (tribe {Tribe})",
                    player.CharacterId, slotIndex, player.Tribe);
                return new TribeVoteActionResult(TribeVoteAction.Accept, 0);
            case TribeVoteCastOutcome.AlreadyVotedThisWindow:
                logger.LogDebug(
                    "Character {CharacterId} Force Leader vote rejected: already voted this window",
                    player.CharacterId);
                return new TribeVoteActionResult(TribeVoteAction.RejectNoAbort, 1);
            default:
                logger.LogDebug("Character {CharacterId} Force Leader vote rejected ({Outcome})", player.CharacterId,
                    voteOutcome);
                return TribeVoteActionResult.Aborted;
        }
    }
}
