namespace Fenrir.Application.Game.Guilds;

/// <summary>Soft outcomes of CZ_GUILD_ASK_SEND -- mirrors ZC_GUILD_ANSWER_RECV's pre-check codes (contracts/06_guild_tribe.md, doc 10 §1).</summary>
public enum GuildInviteAskOutcome
{
    Sent,
    AskerBusy, // code 3
    TargetBusy // code 5
}

/// <summary>
///     Process-wide guild-invitation negotiation authority (Phase C/V7 Guilds &amp; Tribes, CZ_GUILD_ASK/
///     CANCEL/ANSWER family, opcodes 72-74 -- doc 10 §0/§1, verified against
///     Server/ts25zone/S04_MyWork02.cpp:9827-9967). Mirrors the legacy's own <c>mGuildProcessState</c>
///     machine (1=asker waiting, 2=target waiting to answer, 3=accepted -- the asker must then send
///     CZ_GUILD_WORK_SEND tSort 3 to finalize) with the exact same shape as
///     <see cref="Social.Friends.FriendRegistry" />'s own ask/cancel/answer negotiation, plus one addition:
///     <see cref="_acceptedFor" /> survives PAST the answer (legacy state 3) because, like a friend accept,
///     acceptance here does not immediately perform the join -- the ASKER (not the target) must separately
///     send GUILD_WORK tSort 3 afterwards to actually finalize it (see <see cref="TryConsumeAccepted" />).
/// </summary>
public sealed class GuildInviteRegistry
{
    private readonly Lock _lock = new();
    private readonly Dictionary<int, int> _pendingByAsker = new();
    private readonly Dictionary<int, int> _pendingByTarget = new();

    /// <summary>askerId -&gt; the target it may now finalize the join for via GUILD_WORK tSort 3 (legacy state 3).</summary>
    private readonly Dictionary<int, int> _acceptedFor = new();

    private bool IsNegotiating(int characterId)
    {
        return _pendingByAsker.ContainsKey(characterId) || _pendingByTarget.ContainsKey(characterId);
    }

    /// <summary>CZ_GUILD_ASK_SEND (opcode 72). Caller has already verified the asker's own role/guild membership and the tribe match.</summary>
    public GuildInviteAskOutcome TryAsk(int askerId, int targetId)
    {
        lock (_lock)
        {
            if (IsNegotiating(askerId))
                return GuildInviteAskOutcome.AskerBusy;
            if (IsNegotiating(targetId))
                return GuildInviteAskOutcome.TargetBusy;

            _pendingByAsker[askerId] = targetId;
            _pendingByTarget[targetId] = askerId;
            return GuildInviteAskOutcome.Sent;
        }
    }

    /// <summary>CZ_GUILD_CANCEL_SEND (opcode 73) -- withdraws the caller's own still-pending ask (legacy: silent no-op if not in state 1).</summary>
    public bool TryCancel(int askerId, out int targetId)
    {
        lock (_lock)
        {
            if (!_pendingByAsker.Remove(askerId, out targetId))
                return false;

            _pendingByTarget.Remove(targetId);
            return true;
        }
    }

    /// <summary>CZ_GUILD_ANSWER_SEND (opcode 74). 0=accept promotes both sides to legacy state 3 (and remembers <paramref name="targetId" />'s acceptance for the ASKER's own later finalize); 1/2=refuse resets both to state 0.</summary>
    public bool TryAnswer(int targetId, bool accepted, out int askerId)
    {
        lock (_lock)
        {
            if (!_pendingByTarget.Remove(targetId, out askerId))
                return false;

            _pendingByAsker.Remove(askerId);

            if (accepted)
                _acceptedFor[askerId] = targetId;

            return true;
        }
    }

    /// <summary>
    ///     GUILD_WORK tSort 3 (invite finalize)'s own precondition ("both sides in state 3") -- the ASKER
    ///     consumes this, not the target (doc 10 §1 tSort 3: "l'inviteur devra envoyer CZ 75 tSort 3"). No
    ///     name/id travels on that request's wire payload (tData is ignored for tSort 3), so the target is
    ///     whichever character <paramref name="askerId" /> most recently had an accepted answer with.
    ///     Consumed (cleared) on success -- a repeat GUILD_WORK tSort 3 with nothing pending correctly fails.
    /// </summary>
    public bool TryConsumeAccepted(int askerId, out int targetId)
    {
        lock (_lock)
        {
            return _acceptedFor.Remove(askerId, out targetId);
        }
    }
}
