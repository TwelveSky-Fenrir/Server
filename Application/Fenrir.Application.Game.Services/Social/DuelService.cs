using Fenrir.Application.Game.Abstractions.Social;
using Fenrir.Application.Game.Domain.Social.Duel;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Network.Serialization.Zone.Packets.Zone;
using Microsoft.Extensions.Logging;

namespace Fenrir.Application.Game.Services.Social;

/// <inheritdoc cref="IDuelService" />
public sealed class DuelService(ZoneRegistry zones, DuelRegistry duels, ILogger<DuelService> logger) : IDuelService
{
    /// <remarks>
    ///     Réf. C++ : Server/ts25zone/S04_MyWork02.cpp:8259-8277,8459-8471,9088-9101,9311-9324 (the shared
    ///     CZ_DUEL_ASK_SEND/CZ_FRIEND_ASK_SEND/CZ_PARTY_ASK_SEND/CZ_TEACHER_ASK_SEND/CZ_TRADE_ASK_SEND
    ///     pre-check family) -- legacy checks the requester's OWN busy/pose state (still dueling, still
    ///     negotiating another ask) before it ever resolves the target avatar by name, so a busy challenger
    ///     asking a nonexistent/offline name gets the busy reply, not "target not found". The two
    ///     challenger-side checks below are therefore ordered ahead of <see cref="FindPlayerByName" />; the
    ///     equivalent checks inside <see cref="DuelRegistry.TryAsk" /> stay in place for the actual
    ///     registration (still race-safe under its own lock) and only matter now for a busy state that
    ///     changed in the narrow window between the two checks.
    /// </remarks>
    public DuelAskResultKind Ask(Zone zone, PlayerRuntimeState challenger, string targetAvatarName, int sort)
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
            // Desynced/leaked is-dueling flag -- an anti-cheat gate, not an ordinary busy reply; the caller
            // (DuelAskHandler) terminates the challenger's own session for this outcome.
            logger.LogInformation(
                "Duel ask rejected and challenger {ChallengerId}'s session will be terminated: challenger is already actively dueling (desynced state)",
                challenger.CharacterId);
            return DuelAskResultKind.ChallengerAlreadyDueling;
        }

        if (duels.IsNegotiating(challenger.CharacterId))
        {
            logger.LogInformation(
                "Duel ask rejected: challenger {ChallengerId} is already negotiating another duel",
                challenger.CharacterId);
            return DuelAskResultKind.ChallengerBusy;
        }

        var target = FindPlayerByName(zone, targetAvatarName);
        if (target is null)
        {
            logger.LogInformation(
                "Duel ask rejected: challenger {ChallengerId} named target avatar {TargetAvatarName}, not found in zone {MapId}",
                challenger.CharacterId, targetAvatarName, zone.MapId);
            return DuelAskResultKind.TargetNotFound;
        }

        var interTribeAllowed = zone.MapId is 37 or 119 or 124;
        if (!interTribeAllowed && challenger.Tribe != target.Tribe)
        {
            // Business-rule gate, not an ordinary busy reply -- the caller (DuelAskHandler) terminates the
            // challenger's own session for this outcome, same posture as ChallengerAlreadyDueling above.
            logger.LogInformation(
                "Duel ask rejected and challenger {ChallengerId}'s session will be terminated: cross-tribe duel with target {TargetId} not allowed on map {MapId}",
                challenger.CharacterId, target.CharacterId, zone.MapId);
            return DuelAskResultKind.TribeMismatch;
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
                // DuelAskOutcome is fully covered by the cases above -- reachable only if that enum ever
                // grows a new member without a matching case here. Logged loudly since it signals a real gap,
                // not a routine business outcome.
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
            // Benign race, not a rejection: the challenger may have already canceled, or this is a stale
            // resend -- nothing was pending, so there is no state change to report at Information level.
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

        // duel.RemainingTicks is the single source of truth for the 180-legacy-tick countdown (see
        // DuelRegistry.DurationTicks/ActiveDuel.RemainingTicks's own remarks) -- freshly seeded by TryStart,
        // never a separately-duplicated literal here.
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

    private static PlayerRuntimeState? FindPlayerByName(Zone zone, string avatarName)
    {
        foreach (var candidate in zone.Players)
            if (string.Equals(candidate.Name, avatarName, StringComparison.OrdinalIgnoreCase))
                return candidate;

        return null;
    }
}
