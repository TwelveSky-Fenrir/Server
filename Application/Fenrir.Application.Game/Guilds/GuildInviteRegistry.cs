namespace Fenrir.Application.Game.Guilds;

/// <summary>Soft outcomes of CZ_GUILD_ASK_SEND -- mirrors ZC_GUILD_ANSWER_RECV's pre-check codes.</summary>
public enum GuildInviteAskOutcome
{
    Sent,
    AskerBusy, // code 3
    TargetBusy // code 5
}

/// <summary>
///     Process-wide guild-invitation negotiation authority. Mirrors the legacy's <c>mGuildProcessState</c> machine
///     (1=asker waiting, 2=target waiting, 3=accepted), with <see cref="_acceptedFor" /> surviving past the answer.
/// </summary>
public sealed class GuildInviteRegistry
{
    /// <summary>askerId -&gt; the target it may now finalize the join for (legacy state 3).</summary>
    private readonly Dictionary<int, int> _acceptedFor = new();

    private readonly Lock _lock = new();
    private readonly Dictionary<int, int> _pendingByAsker = new();
    private readonly Dictionary<int, int> _pendingByTarget = new();

    private bool IsNegotiating(int characterId)
    {
        return _pendingByAsker.ContainsKey(characterId) || _pendingByTarget.ContainsKey(characterId);
    }

    /// <summary>Caller has already verified the asker's own role/guild membership and the tribe match.</summary>
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

    /// <summary>Withdraws the caller's own still-pending ask -- silent no-op if not currently pending.</summary>
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

    /// <summary>
    ///     Accept promotes both sides to legacy state 3 and remembers the acceptance for the asker's later finalize;
    ///     refuse resets both to state 0.
    /// </summary>
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
    ///     The asker consumes this, not the target -- the finalize request's payload carries no name/id, so the target is
    ///     whoever most recently accepted.
    /// </summary>
    public bool TryConsumeAccepted(int askerId, out int targetId)
    {
        lock (_lock)
        {
            return _acceptedFor.Remove(askerId, out targetId);
        }
    }
}
