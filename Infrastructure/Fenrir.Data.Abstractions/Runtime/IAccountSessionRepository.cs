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
    ConflictTearingDown = 3,

    /// <summary>
    ///     The existing row was Game-side (steady state, not mid-teardown) but its own ShardId had no live
    ///     entry in runtime.GameServerDirectory (or one whose heartbeat exceeded the established 15-second
    ///     staleness threshold) -- the shard is provably dead, so usp_AccountSession_ClaimOrSignalKick fast-
    ///     cleared the row and registered this claim in its place immediately, instead of merely flagging
    ///     KickRequested for a process that will never service it (see
    ///     Database/Migrations/023_account_session_dead_shard_fast_reclaim.sql). PreviousShardId carries the
    ///     dead shard's id for logging. The caller should treat this exactly like <see cref="Registered" />
    ///     (the login proceeds) while logging the distinction for operational visibility.
    /// </summary>
    ReclaimedDeadShard = 4
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
    ///     correct side effect (register, clear a stale Login row, fast-reclaim a Game row whose owning shard is
    ///     provably dead, flag a still-live Game row for kick, or refuse a tearing-down row) so two near-simultaneous
    ///     logins for the same account can't both win. The loser of a genuine race gets a native-compiled
    ///     write-write conflict from SQL Server itself (error 41302, or a dependency failure 41305/41325); the
    ///     implementation retries a small bounded number of times on those specific errors before letting them
    ///     propagate. Each retry passes its own 1-based attempt number so the procedure can tell a row just
    ///     committed by a genuinely concurrent winner (only ever seen on a retry, since that is the only way to
    ///     reach one) apart from a truly stale row abandoned by an unrelated earlier session (only ever seen on the
    ///     first attempt) -- see Migrations/020_account_session_claim_race_fix.sql for the race this closes.
    /// </summary>
    /// <remarks>
    ///     Since Database/Migrations/023_account_session_dead_shard_fast_reclaim.sql, the Game-side branch first
    ///     cross-checks runtime.GameServerDirectory's heartbeat for the row's own ShardId (the same 15-second
    ///     staleness threshold usp_GameServer_GetDirectory/usp_CharacterShardLocation_FindByCharacterId already
    ///     use) before falling back to the old flag-for-kick behavior -- see
    ///     <see cref="AccountSessionClaimOutcome.ReclaimedDeadShard" />.
    /// </remarks>
    public ValueTask<AccountSessionClaimDto> ClaimOrSignalKickAsync(int accountId, Guid newSessionToken,
        CancellationToken ct);

    /// <summary>
    ///     usp_AccountSession_TransitionToGame: called at world-entry. Accepted only when the account still holds the
    ///     Login-side session matching expectedSessionToken -- proves this claim is for the same login epoch as the
    ///     one that minted the SessionTicket carrying that token, not a hijack of a newer login.
    /// </summary>
    public ValueTask<bool> TransitionToGameAsync(int accountId, Guid expectedSessionToken, byte shardId,
        CancellationToken ct);

    /// <summary>
    ///     usp_AccountSession_MarkTearingDown: idempotent no-op gated on (ServerKind, ShardId, SessionToken) all
    ///     matching -- identical ownership match to <see cref="ClearIfOwnerAsync" />, the step immediately
    ///     following it in every caller's teardown sequence. A row already reassigned to a different owner by an
    ///     independent event (most commonly this same account's Game-side world-entry claim completing
    ///     concurrently with a Login-side disconnect) is left untouched instead of being marked tearing-down out
    ///     from under its new owner (see Migrations/022_account_session_mark_tearing_down_gate.sql for the race
    ///     this closes).
    /// </summary>
    public ValueTask MarkTearingDownAsync(int accountId, AccountSessionServerKind serverKind, byte? shardId,
        Guid sessionToken, CancellationToken ct);

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
