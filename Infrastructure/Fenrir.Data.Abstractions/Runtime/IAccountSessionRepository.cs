using System.Collections.Immutable;

namespace Fenrir.Data.Abstractions.Runtime;

public enum AccountSessionServerKind : byte
{
    Login = 0,
    Game = 1
}

public enum AccountSessionClaimOutcome : byte
{
    Registered = 0,
    ConflictLogin = 1,
    ConflictGameKicked = 2,
    ConflictTearingDown = 3
}

/// <summary>
///     Single cross-process authority for "does this account already have a session, and where"
///     (runtime.AccountSessions) -- the mechanism behind cross-process duplicate-login kick/refusal. Every
///     successful credential check calls <see cref="ClaimOrSignalKickAsync" /> exactly once; a Game-side
///     world-entry re-check defers entirely to <see cref="TransitionToGameAsync" /> rather than a second
///     decision tree.
/// </summary>
public interface IAccountSessionRepository
{
    /// <summary>
    ///     usp_AccountSession_ClaimOrSignalKick: reads the existing row and, in the same atomic block, performs the
    ///     correct side effect (register, clear a stale Login row, flag a live Game row for kick, or refuse a
    ///     tearing-down row) so two near-simultaneous logins for the same account can't both win. The loser of a
    ///     genuine race gets a native-compiled write-write conflict from SQL Server itself (error 41302, or a
    ///     dependency failure 41305/41325); the implementation retries a small bounded number of times on those
    ///     specific errors before letting them propagate.
    /// </summary>
    public ValueTask<AccountSessionClaimDto> ClaimOrSignalKickAsync(int accountId, Guid newSessionToken,
        CancellationToken ct);

    /// <summary>
    ///     usp_AccountSession_TransitionToGame: called at world-entry. Accepted only when the account still holds the
    ///     Login-side session matching expectedSessionToken -- proves this claim is for the same login epoch as the
    ///     one that minted the SessionTicket carrying that token, not a hijack of a newer login.
    /// </summary>
    public ValueTask<bool> TransitionToGameAsync(int accountId, Guid expectedSessionToken, byte shardId,
        CancellationToken ct);

    /// <summary>usp_AccountSession_MarkTearingDown: best-effort, no-op if the row is already gone.</summary>
    public ValueTask MarkTearingDownAsync(int accountId, CancellationToken ct);

    /// <summary>
    ///     usp_AccountSession_ClearIfOwner: idempotent delete gated on (ServerKind, ShardId, SessionToken) all
    ///     matching -- a row that already moved on (different ServerKind/ShardId) or was superseded by a newer token
    ///     correctly no-ops instead of deleting someone else's live row.
    /// </summary>
    public ValueTask ClearIfOwnerAsync(int accountId, AccountSessionServerKind serverKind, byte? shardId,
        Guid sessionToken, CancellationToken ct);

    /// <summary>
    ///     usp_AccountSession_RefreshAndGetKicked: batched per-shard/per-process poll. Refreshes LastRefreshedUtc for
    ///     every account id supplied (the 6-minute staleness sweep depends on this), then returns the subset that has
    ///     KickRequested set. Always returns empty for AccountSessionServerKind.Login by construction. Never called
    ///     with an empty accountIds collection -- callers must check for that themselves (batched poll hosts skip the
    ///     round trip entirely when they hold no local sessions).
    /// </summary>
    public ValueTask<ImmutableArray<KickedAccountDto>> RefreshAndGetKickedAsync(AccountSessionServerKind serverKind,
        byte? shardId, IReadOnlyCollection<int> accountIds, CancellationToken ct);

    /// <summary>
    ///     usp_AccountSession_ReapStale: deletes every row whose LastRefreshedUtc is older than 6 minutes and returns
    ///     which accounts were reaped and from which ServerKind, so the caller can log the two distinct forced-timeout
    ///     paths. Called from a single unsharded timer (Login side).
    /// </summary>
    public ValueTask<ImmutableArray<ReapedAccountSessionDto>> ReapStaleAsync(CancellationToken ct);

    /// <summary>
    ///     usp_AccountSession_GetActiveCount: a plain COUNT(*) across every row regardless of ServerKind/ShardId --
    ///     the cluster-wide "concurrent player" tally the login-time server-full quota gate reads (every zone's
    ///     Game-side rows plus every Login-side row for an account registered but not yet moved into a zone).
    ///     Fenrir's stand-in for the legacy session-broker process (<c>ts25playuser</c>'s zero-argument
    ///     <c>ReturnPresentUserNum</c>): this table is already the single cross-process authority for "does this
    ///     account have a session, and where", so a fresh broker process isn't needed to answer "how many".
    /// </summary>
    public ValueTask<int> GetActiveSessionCountAsync(CancellationToken ct);
}
