using Fenrir.Application.Game.Domain.World;

namespace Fenrir.Application.Game.Abstractions.Social;

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
    public DuelAskResultKind Ask(Zone zone, PlayerRuntimeState challenger, string targetAvatarName, int sort);

    /// <summary>CZ_DUEL_ANSWER_SEND -- notifies the challenger of the outcome; no-op if nothing was pending.</summary>
    public void Answer(int targetId, int answerCode);

    /// <summary>CZ_DUEL_CANCEL_SEND -- withdraws the caller's own still-pending ask.</summary>
    public void Cancel(int challengerId);

    /// <summary>CZ_DUEL_START_SEND -- callable by either accepted side; arms and notifies both sides.</summary>
    public void Start(int callerId);
}
