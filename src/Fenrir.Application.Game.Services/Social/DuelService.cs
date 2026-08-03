using Fenrir.Application.Game.Abstractions.Social;
using Fenrir.Application.Game.Domain.Guilds;
using Fenrir.Application.Game.Domain.Social;
using Fenrir.Application.Game.Domain.Social.Duel;
using Fenrir.Application.Game.Domain.Social.Friends;
using Fenrir.Application.Game.Domain.Social.Mentor;
using Fenrir.Application.Game.Domain.Social.Party;
using Fenrir.Application.Game.Domain.Social.Trade;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Protocol.Game;
using Microsoft.Extensions.Logging;

namespace Fenrir.Application.Game.Services.Social;

public sealed class DuelService(
    ZoneRegistry zones,
    DuelRegistry duels,
    TradeRegistry trades,
    FriendRegistry friends,
    PartyRegistry parties,
    MentorRegistry mentors,
    GuildInviteRegistry guildInvites,
    ILogger<DuelService> logger) : IDuelService
{
    public ValueTask<DuelAskResultKind> AskAsync(Zone zone, PlayerRuntimeState challenger,
        string targetAvatarName, int sort, CancellationToken cancellationToken)
    {
        if (zone.MapId == 124)
        {
            logger.LogInformation(
                "Duel ask rejected: challenger {ChallengerId} is on map {MapId}, which forbids duels",
                challenger.CharacterId, zone.MapId);
            return ValueTask.FromResult(DuelAskResultKind.MapForbidden);
        }

        if (duels.TryGetActiveDuel(challenger.CharacterId, out _))
        {
            logger.LogInformation(
                "Duel ask rejected and challenger {ChallengerId}'s session terminated: challenger is already actively dueling (desynced client)",
                challenger.CharacterId);
            challenger.Session.Abort(DisconnectReason.StateViolation);
            return ValueTask.FromResult(DuelAskResultKind.ChallengerAlreadyDueling);
        }

        if (CommunityWorkGate.IsBusy(challenger, duels, trades, friends, parties, mentors, guildInvites))
        {
            logger.LogInformation(
                "Duel ask rejected: challenger {ChallengerId} is already negotiating another duel",
                challenger.CharacterId);
            return ValueTask.FromResult(DuelAskResultKind.ChallengerBusy);
        }

        if (challenger.IsStunned || challenger.IsDead)
        {
            logger.LogInformation(
                "Duel ask rejected: challenger {ChallengerId} is stunned or dead",
                challenger.CharacterId);
            return ValueTask.FromResult(DuelAskResultKind.ChallengerBusy);
        }

        if (!zones.TryGetPlayerByName(targetAvatarName, out var target))
        {
            logger.LogInformation(
                "Duel ask rejected: challenger {ChallengerId} named target avatar {TargetAvatarName}, not found on this shard",
                challenger.CharacterId, targetAvatarName);
            return ValueTask.FromResult(DuelAskResultKind.TargetNotFound);
        }

        var interTribeAllowed = zone.MapId is 37 or 119 or 124;
        if (!interTribeAllowed && challenger.Tribe != target.Tribe)
        {
            logger.LogInformation(
                "Duel ask rejected and challenger {ChallengerId}'s session terminated: cross-tribe duel with target {TargetId} not allowed on map {MapId} (desynced client)",
                challenger.CharacterId, target.CharacterId, zone.MapId);
            challenger.Session.Abort(DisconnectReason.StateViolation);
            return ValueTask.FromResult(DuelAskResultKind.TribeMismatch);
        }

        if (CommunityWorkGate.IsBusy(target, duels, trades, friends, parties, mentors, guildInvites) ||
            target.IsMovingZone)
        {
            logger.LogInformation("Duel ask rejected: target {TargetId} is busy (community-work gate or zone transfer)",
                target.CharacterId);
            return ValueTask.FromResult(DuelAskResultKind.TargetBusy);
        }

        if (target.IsStunned || target.IsDead)
        {
            logger.LogInformation("Duel ask rejected: target {TargetId} is stunned or dead", target.CharacterId);
            return ValueTask.FromResult(DuelAskResultKind.TargetBusy);
        }

        switch (duels.TryAsk(challenger.CharacterId, target.CharacterId, sort == 1))
        {
            case DuelAskOutcome.ChallengerAlreadyDueling:
                logger.LogInformation(
                    "Duel ask rejected and challenger {ChallengerId}'s session terminated: challenger is already actively dueling (desynced client, caught at registration)",
                    challenger.CharacterId);
                challenger.Session.Abort(DisconnectReason.StateViolation);
                return ValueTask.FromResult(DuelAskResultKind.ChallengerAlreadyDueling);
            case DuelAskOutcome.ChallengerBusy:
                logger.LogInformation(
                    "Duel ask rejected: challenger {ChallengerId} is already negotiating another duel (caught at registration)",
                    challenger.CharacterId);
                return ValueTask.FromResult(DuelAskResultKind.ChallengerBusy);
            case DuelAskOutcome.TargetBusy:
                logger.LogInformation(
                    "Duel ask rejected: target {TargetId} is busy (negotiating or actively dueling)",
                    target.CharacterId);
                return ValueTask.FromResult(DuelAskResultKind.TargetBusy);
            case DuelAskOutcome.Sent:
                logger.LogInformation(
                    "Duel challenge sent: challenger {ChallengerId} -> target {TargetId} (sort {Sort}, noPotions {NoPotions})",
                    challenger.CharacterId, target.CharacterId, sort, sort == 1);
                target.Session.Send(new DuelChallengeResponse { AvatarName = challenger.Name, Sort = sort });
                return ValueTask.FromResult(DuelAskResultKind.Sent);
            default:
                logger.LogWarning(
                    "Duel ask for challenger {ChallengerId} hit an unexpected DuelRegistry.TryAsk outcome -- treating as ChallengerBusy",
                    challenger.CharacterId);
                return ValueTask.FromResult(DuelAskResultKind.ChallengerBusy);
        }
    }

    public void Answer(int targetId, int answerCode)
    {
        if (!duels.TryAnswer(targetId, answerCode == 0, out var challengerId))
        {
            logger.LogDebug(
                "Duel answer ignored: no pending ask found for target {TargetId} (answer code {AnswerCode})",
                targetId, answerCode);
            return;
        }

        var accepted = answerCode == 0;
        logger.LogInformation(
            "Duel challenge {Outcome}: target {TargetId} answered challenger {ChallengerId} (code {AnswerCode})",
            accepted ? "accepted" : "declined", targetId, challengerId, answerCode);

        if (!zones.TryGetPlayer(challengerId, out var challenger))
        {
            logger.LogWarning(
                "Duel answer for challenger {ChallengerId} could not be delivered: challenger not found in any zone on this shard",
                challengerId);
            return;
        }

        if (challenger.IsMovingZone)
        {
            logger.LogInformation(
                "Duel answer for challenger {ChallengerId} withheld: challenger is mid zone-transfer",
                challengerId);
            return;
        }

        challenger.Session.Send(new DuelAnswerResponse { Answer = answerCode });
    }

    public void Cancel(int challengerId)
    {
        if (!duels.TryCancel(challengerId, out var targetId))
        {
            logger.LogDebug("Duel cancel ignored: no pending ask found for challenger {ChallengerId}", challengerId);
            return;
        }

        logger.LogInformation(
            "Duel challenge canceled: challenger {ChallengerId} withdrew ask to target {TargetId}",
            challengerId, targetId);

        if (!zones.TryGetPlayer(targetId, out var target))
        {
            logger.LogWarning(
                "Duel cancel notification for target {TargetId} could not be delivered: target not found in any zone on this shard",
                targetId);
            return;
        }

        if (target.IsMovingZone)
        {
            logger.LogInformation(
                "Duel cancel notification for target {TargetId} withheld: target is mid zone-transfer", targetId);
            return;
        }

        target.Session.Send(new DuelCancelResponse());
    }

    public void Start(int callerId)
    {
        if (!duels.TryStart(callerId, out var duel))
        {
            logger.LogDebug("Duel start ignored: no accepted duel pending for caller {CallerId}", callerId);
            return;
        }

        if (!zones.TryGetPlayerAndZone(duel.PlayerA, out var playerA, out var requesterZone) ||
            !zones.TryGetPlayer(duel.PlayerB, out var playerB))
        {
            logger.LogWarning(
                "Duel {UniqueNumber} start aborted: a participant was not found in any zone on this shard (playerA {PlayerA}, playerB {PlayerB})",
                duel.UniqueNumber, duel.PlayerA, duel.PlayerB);
            return;
        }

        if (playerA.IsMovingZone || playerB.IsMovingZone)
        {
            duels.TryEndActiveDuel(callerId, out _);
            logger.LogInformation(
                "Duel {UniqueNumber} start aborted and registration rolled back: participant {PlayerA} (moving zone {PlayerAMoving}) / {PlayerB} (moving zone {PlayerBMoving}) is mid zone-transfer",
                duel.UniqueNumber, duel.PlayerA, playerA.IsMovingZone, duel.PlayerB, playerB.IsMovingZone);
            return;
        }

        var eatDrugState = duel.NoPotions ? 1 : 0;

        logger.LogInformation(
            "Duel {UniqueNumber} started: {PlayerA} vs {PlayerB} (noPotions {NoPotions}, remainingTicks {RemainingTicks})",
            duel.UniqueNumber, duel.PlayerA, duel.PlayerB, duel.NoPotions, duel.RemainingTicks);

        playerA.Session.Send(new DuelStartResponse
        {
            DuelState = [1, duel.UniqueNumber, 1],
            RemainTime = duel.RemainingTicks,
            EatDrugState = eatDrugState
        });

        playerB.Session.Send(new DuelStartResponse
        {
            DuelState = [1, duel.UniqueNumber, 2],
            RemainTime = duel.RemainingTicks,
            EatDrugState = eatDrugState
        });

        if (!requesterZone.Post(ZoneCommand.BroadcastDuelStart(duel.PlayerA, duel.PlayerB, duel.UniqueNumber)))
            logger.LogWarning(
                "Zone {MapId} inbox full: dropped duel-start broadcast for duel {UniqueNumber}",
                requesterZone.MapId, duel.UniqueNumber);
    }
}
