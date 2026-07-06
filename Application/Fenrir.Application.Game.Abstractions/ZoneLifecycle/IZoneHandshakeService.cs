namespace Fenrir.Application.Game.Abstractions.ZoneLifecycle;

public enum ZoneHandshakeOutcome
{
    Rejected,

    Accepted,

    /// <summary>
    ///     The ticket itself was valid (right account, right shard) but <c>usp_AccountSession_TransitionToGame</c>
    ///     refused it -- the Login-side <c>runtime.AccountSessions</c> row no longer matches the session token this
    ///     ticket carried (a newer login already claimed the account). Handled identically to a hijack attempt: no
    ///     response packet, the connection is simply dropped.
    /// </summary>
    SessionSuperseded
}

public readonly record struct ZoneHandshakeResult(
    ZoneHandshakeOutcome Outcome,
    int AccountId = 0,
    int CharacterId = 0,
    Guid SessionToken = default);

/// <summary>
///     Business logic for op11, the first packet after ZC_CONNECT_OK_RECV: consumes the single-use session
///     ticket the LoginServer minted for this AccountId (ADR-0005) -- see <c>ZoneHandshakeHandler</c>'s remarks.
/// </summary>
public interface IZoneHandshakeService
{
    public ValueTask<ZoneHandshakeResult> ConsumeTicketAsync(string obfuscatedId, CancellationToken cancellationToken);
}
