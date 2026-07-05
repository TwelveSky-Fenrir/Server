using Fenrir.Application.Game.Social.Duel;
using Fenrir.Application.Game.World;
using Fenrir.Contracts.Packets.Zone;

namespace Fenrir.Application.Game.Handlers.Social.Services;

/// <summary>Outcome of CZ_DUEL_ASK_SEND's pre-checks, as branched on by <see cref="DuelAskHandler" />.</summary>
public enum DuelAskResultKind
{
    MapForbidden, // map 124 (scripted-duel server) always refuses immediately
    TargetNotFound,
    TribeMismatch,
    ChallengerBusy,
    TargetBusy,
    Sent
}

/// <summary>Business logic behind the CZ_DUEL_* opcode family, extracted from the Duel*Handlers.</summary>
public interface IDuelService
{
    /// <summary>CZ_DUEL_ASK_SEND -- on <see cref="DuelAskResultKind.Sent" />, the target has already been notified.</summary>
    DuelAskResultKind Ask(Zone zone, PlayerRuntimeState challenger, string targetAvatarName, int sort);

    /// <summary>CZ_DUEL_ANSWER_SEND -- notifies the challenger of the outcome; no-op if nothing was pending.</summary>
    void Answer(int targetId, int answerCode);

    /// <summary>CZ_DUEL_CANCEL_SEND -- withdraws the caller's own still-pending ask.</summary>
    void Cancel(int challengerId);

    /// <summary>CZ_DUEL_START_SEND -- callable by either accepted side; arms and notifies both sides.</summary>
    void Start(int callerId);
}

/// <inheritdoc cref="IDuelService" />
public sealed class DuelService(ZoneRegistry zones, DuelRegistry duels) : IDuelService
{
    private const int DurationSeconds = 180;

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

        playerA.Session.Send(new DuelStartResponse
        {
            DuelState = [1, duel.UniqueNumber, 1],
            RemainTime = DurationSeconds,
            EatDrugState = eatDrugState
        });

        playerB.Session.Send(new DuelStartResponse
        {
            DuelState = [1, duel.UniqueNumber, 2],
            RemainTime = DurationSeconds,
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
