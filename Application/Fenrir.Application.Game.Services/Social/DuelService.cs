using Fenrir.Application.Game.Abstractions.Social;
using Fenrir.Application.Game.Domain.Social.Duel;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Network.Serialization.Packets.Zone;

namespace Fenrir.Application.Game.Services.Social;

/// <inheritdoc cref="IDuelService" />
public sealed class DuelService(ZoneRegistry zones, DuelRegistry duels) : IDuelService
{
    public DuelAskResultKind Ask(Zone zone, PlayerRuntimeState challenger, string targetAvatarName, int sort)
    {
        if (zone.MapId == 124)
            return DuelAskResultKind.MapForbidden;

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
