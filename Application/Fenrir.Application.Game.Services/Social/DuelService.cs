using Fenrir.Application.Game.Abstractions.Social;
using Fenrir.Application.Game.Domain;
using Fenrir.Application.Game.Domain.Guilds;
using Fenrir.Application.Game.Domain.Social;
using Fenrir.Application.Game.Domain.Social.Duel;
using Fenrir.Application.Game.Domain.Social.Friends;
using Fenrir.Application.Game.Domain.Social.Mentor;
using Fenrir.Application.Game.Domain.Social.Party;
using Fenrir.Application.Game.Domain.Social.Trade;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Network.Serialization.Zone.Packets.Zone;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Fenrir.Application.Game.Services.Social;

public sealed class DuelService(
    ZoneRegistry zones,
    DuelRegistry duels,
    TradeRegistry trades,
    FriendRegistry friends,
    PartyRegistry parties,
    MentorRegistry mentors,
    GuildInviteRegistry guildInvites,
    ICharacterShardLocationRepository characterShardLocations,
    ISocialCrossShardRelayQueue crossShardRelay,
    IOptions<GameServerOptions> options,
    ILogger<DuelService> logger) : IDuelService
{

        public async ValueTask<DuelAskResultKind> AskAsync(Zone zone, PlayerRuntimeState challenger,
        string targetAvatarName, int sort, CancellationToken cancellationToken)
    {
        if (zone.MapId == 124)
        {
            logger.LogInformation(
                "Duel ask rejected: challenger {ChallengerId} is on map {MapId}, which forbids duels",
                challenger.CharacterId, zone.MapId);
            return DuelAskResultKind.MapForbidden;
        }

        if (duels.TryGetActiveDuel(challenger.CharacterId, out _))
        {
            logger.LogInformation(
                "Duel ask rejected and challenger {ChallengerId}'s session will be terminated: challenger is already actively dueling (desynced state)",
                challenger.CharacterId);
            return DuelAskResultKind.ChallengerAlreadyDueling;
        }

        if (CommunityWorkGate.IsBusy(challenger, duels, trades, friends, parties, mentors, guildInvites))
        {
            logger.LogInformation(
                "Duel ask rejected: challenger {ChallengerId} is already negotiating another duel",
                challenger.CharacterId);
            return DuelAskResultKind.ChallengerBusy;
        }

        var target = FindPlayerByName(zone, targetAvatarName);
        if (target is null)
            return await AskCrossShardAsync(challenger, targetAvatarName, cancellationToken).ConfigureAwait(false);

        var interTribeAllowed = zone.MapId is 37 or 119 or 124;
        if (!interTribeAllowed && challenger.Tribe != target.Tribe)
        {
            logger.LogInformation(
                "Duel ask rejected and challenger {ChallengerId}'s session will be terminated: cross-tribe duel with target {TargetId} not allowed on map {MapId}",
                challenger.CharacterId, target.CharacterId, zone.MapId);
            return DuelAskResultKind.TribeMismatch;
        }

        if (CommunityWorkGate.IsBusy(target, duels, trades, friends, parties, mentors, guildInvites))
        {
            logger.LogInformation("Duel ask rejected: target {TargetId} is busy (community-work gate)",
                target.CharacterId);
            return DuelAskResultKind.TargetBusy;
        }

        switch (duels.TryAsk(challenger.CharacterId, target.CharacterId, sort == 1))
        {
            case DuelAskOutcome.ChallengerAlreadyDueling:
                logger.LogInformation(
                    "Duel ask rejected and challenger {ChallengerId}'s session will be terminated: challenger is already actively dueling (desynced state, caught at registration)",
                    challenger.CharacterId);
                return DuelAskResultKind.ChallengerAlreadyDueling;
            case DuelAskOutcome.ChallengerBusy:
                logger.LogInformation(
                    "Duel ask rejected: challenger {ChallengerId} is already negotiating another duel (caught at registration)",
                    challenger.CharacterId);
                return DuelAskResultKind.ChallengerBusy;
            case DuelAskOutcome.TargetBusy:
                logger.LogInformation(
                    "Duel ask rejected: target {TargetId} is busy (negotiating or actively dueling)",
                    target.CharacterId);
                return DuelAskResultKind.TargetBusy;
            case DuelAskOutcome.Sent:
                logger.LogInformation(
                    "Duel challenge sent: challenger {ChallengerId} -> target {TargetId} (sort {Sort}, noPotions {NoPotions})",
                    challenger.CharacterId, target.CharacterId, sort, sort == 1);
                target.Session.Send(new DuelChallengeResponse { AvatarName = challenger.Name, Sort = sort });
                return DuelAskResultKind.Sent;
            default:
                logger.LogWarning(
                    "Duel ask for challenger {ChallengerId} hit an unexpected DuelRegistry.TryAsk outcome -- treating as ChallengerBusy",
                    challenger.CharacterId);
                return DuelAskResultKind.ChallengerBusy;
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

        if (zones.TryGetPlayer(challengerId, out var challenger))
            challenger.Session.Send(new DuelAnswerResponse { Answer = answerCode });
        else
            logger.LogWarning(
                "Duel answer for challenger {ChallengerId} could not be delivered: challenger not found in any zone on this shard",
                challengerId);
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

        if (zones.TryGetPlayer(targetId, out var target))
            target.Session.Send(new DuelCancelResponse());
        else
            logger.LogWarning(
                "Duel cancel notification for target {TargetId} could not be delivered: target not found in any zone on this shard",
                targetId);
    }

    public void Start(int callerId)
    {
        if (!duels.TryStart(callerId, out var duel))
        {
            logger.LogDebug("Duel start ignored: no accepted duel pending for caller {CallerId}", callerId);
            return;
        }

        if (!zones.TryGetPlayer(duel.PlayerA, out var playerA) || !zones.TryGetPlayer(duel.PlayerB, out var playerB))
        {
            logger.LogWarning(
                "Duel {UniqueNumber} start aborted: a participant was not found in any zone on this shard (playerA {PlayerA}, playerB {PlayerB})",
                duel.UniqueNumber, duel.PlayerA, duel.PlayerB);
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
    }

        private async ValueTask<DuelAskResultKind> AskCrossShardAsync(PlayerRuntimeState challenger,
        string targetAvatarName, CancellationToken cancellationToken)
    {
        var remote = await characterShardLocations.FindByNameAsync(targetAvatarName, cancellationToken)
            .ConfigureAwait(false);

        if (remote is null)
        {
            logger.LogInformation(
                "Duel ask rejected: challenger {ChallengerId} named target avatar {TargetAvatarName}, not found on any shard",
                challenger.CharacterId, targetAvatarName);
            return DuelAskResultKind.TargetNotFound;
        }

        var interTribeAllowed = challenger.MapId is 37 or 119 or 124;
        if (!interTribeAllowed && challenger.Tribe != remote.Tribe)
        {
            logger.LogInformation(
                "Duel ask rejected and challenger {ChallengerId}'s session will be terminated: cross-tribe duel with cross-shard target {TargetId} not allowed on map {MapId}",
                challenger.CharacterId, remote.CharacterId, challenger.MapId);
            return DuelAskResultKind.TribeMismatch;
        }

        var outcome = duels.TryAskCrossShard(challenger.CharacterId,
            new CrossShardOutboundAsk(remote.ShardId, remote.CharacterId, remote.AvatarName));

        switch (outcome)
        {
            case DuelAskOutcome.ChallengerAlreadyDueling:
                logger.LogInformation(
                    "Duel ask rejected and challenger {ChallengerId}'s session will be terminated: challenger is already actively dueling (desynced state, cross-shard registration)",
                    challenger.CharacterId);
                return DuelAskResultKind.ChallengerAlreadyDueling;
            case DuelAskOutcome.Sent:
                crossShardRelay.Enqueue(new SocialCrossShardRelayEntry(
                    SocialCrossShardRelayKind.Duel,
                    SocialCrossShardRelayMessageType.Ask,
                    null,
                    null,
                    options.Value.ShardId,
                    challenger.CharacterId,
                    challenger.Name,
                    remote.ShardId,
                    remote.CharacterId,
                    null));

                logger.LogInformation(
                    "Duel challenge published cross-shard: challenger {ChallengerId} -> target {TargetCharacterId} on shard {TargetShardId} (never delivered today -- see DuelService's own remarks)",
                    challenger.CharacterId, remote.CharacterId, remote.ShardId);
                return DuelAskResultKind.SentCrossShard;
            default:
                logger.LogInformation(
                    "Duel ask rejected: challenger {ChallengerId} is already negotiating another duel (cross-shard registration)",
                    challenger.CharacterId);
                return DuelAskResultKind.ChallengerBusy;
        }
    }

    private static PlayerRuntimeState? FindPlayerByName(Zone zone, string avatarName)
    {
        foreach (var candidate in zone.Players)
            if (string.Equals(candidate.Name, avatarName, StringComparison.OrdinalIgnoreCase))
                return candidate;

        return null;
    }
}
