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
    Sent,

    /// <summary>
    ///     The requester's own is-dueling indicator is still set (an Active duel that never cleared) --
    ///     treated as a desynced client state, not an ordinary busy reply: the requester's own session is
    ///     terminated instead of answered (Server/ts25zone/S04_MyWork02.cpp:8259-8263).
    /// </summary>
    ChallengerAlreadyDueling,

    /// <summary>
    ///     WS1.4: the target was not found on this shard's own <c>ZoneRegistry</c> but WAS resolved on a
    ///     different live shard via <c>ICharacterShardLocationRepository</c> -- the challenge has been handed
    ///     to <c>ISocialCrossShardRelayQueue</c> for publish-only cross-shard delivery. UNLIKE Party/Friend,
    ///     no <c>ISocialCrossShardRelayHandler</c> is registered for <c>SocialCrossShardRelayKind.Duel</c> yet,
    ///     so this challenge is never actually delivered to the target today -- see <c>DuelService.AskAsync</c>'s
    ///     own remarks for why this is still safe (the challenger is never left permanently busy) and what a
    ///     follow-up needs to add to complete the round trip.
    /// </summary>
    SentCrossShard
}

/// <summary>Business logic behind the CZ_DUEL_* opcode family, extracted from the Duel*Handlers.</summary>
public interface IDuelService
{
    /// <summary>
    ///     CZ_DUEL_ASK_SEND -- on <see cref="DuelAskResultKind.Sent" />, the target has already been
    ///     notified. Same-shard lookup first (within <paramref name="zone" />), falling back to the
    ///     cross-shard character-location directory on a miss -- see
    ///     <see cref="DuelAskResultKind.SentCrossShard" />.
    /// </summary>
    public ValueTask<DuelAskResultKind> AskAsync(Zone zone, PlayerRuntimeState challenger, string targetAvatarName,
        int sort, CancellationToken cancellationToken);

    /// <summary>CZ_DUEL_ANSWER_SEND -- notifies the challenger of the outcome; no-op if nothing was pending.</summary>
    public void Answer(int targetId, int answerCode);

    /// <summary>CZ_DUEL_CANCEL_SEND -- withdraws the caller's own still-pending ask.</summary>
    public void Cancel(int challengerId);

    /// <summary>CZ_DUEL_START_SEND -- callable by either accepted side; arms and notifies both sides.</summary>
    public void Start(int callerId);
}
