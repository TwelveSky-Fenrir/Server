using Fenrir.Application.Game.Abstractions.Social;
using Fenrir.Application.Game.Domain.Social.Duel;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Network.Serialization.Packets.Zone;

namespace Fenrir.Application.Game.Services.Social;

/// <inheritdoc cref="IDuelService" />
public sealed class DuelService(ZoneRegistry zones, DuelRegistry duels) : IDuelService
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
            return DuelAskResultKind.MapForbidden;

        if (duels.TryGetActiveDuel(challenger.CharacterId, out _))
            return DuelAskResultKind.ChallengerAlreadyDueling;
        if (duels.IsNegotiating(challenger.CharacterId))
            return DuelAskResultKind.ChallengerBusy;

        var target = FindPlayerByName(zone, targetAvatarName);
        if (target is null)
            return DuelAskResultKind.TargetNotFound;

        var interTribeAllowed = zone.MapId is 37 or 119 or 124;
        if (!interTribeAllowed && challenger.Tribe != target.Tribe)
            return DuelAskResultKind.TribeMismatch;

        switch (duels.TryAsk(challenger.CharacterId, target.CharacterId, sort == 1))
        {
            case DuelAskOutcome.ChallengerAlreadyDueling:
                return DuelAskResultKind.ChallengerAlreadyDueling;
            case DuelAskOutcome.ChallengerBusy:
                return DuelAskResultKind.ChallengerBusy;
            case DuelAskOutcome.TargetBusy:
                return DuelAskResultKind.TargetBusy;
            case DuelAskOutcome.Sent:
                target.Session.Send(new DuelChallengeResponse { AvatarName = challenger.Name, Sort = sort });
                return DuelAskResultKind.Sent;
            default:
                return DuelAskResultKind.ChallengerBusy;
        }
    }

    public void Answer(int targetId, int answerCode)
    {
        if (!duels.TryAnswer(targetId, answerCode == 0, out var challengerId))
            return;

        if (zones.TryGetPlayer(challengerId, out var challenger))
            challenger.Session.Send(new DuelAnswerResponse { Answer = answerCode });
    }

    public void Cancel(int challengerId)
    {
        if (!duels.TryCancel(challengerId, out var targetId))
            return;

        if (zones.TryGetPlayer(targetId, out var target))
            target.Session.Send(new DuelCancelResponse());
    }

    public void Start(int callerId)
    {
        if (!duels.TryStart(callerId, out var duel))
            return;

        if (!zones.TryGetPlayer(duel.PlayerA, out var playerA) || !zones.TryGetPlayer(duel.PlayerB, out var playerB))
            return;

        var eatDrugState = duel.NoPotions ? 1 : 0;

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
