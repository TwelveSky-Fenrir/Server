-- Corrective fix for runtime.usp_AccountSession_ClaimOrSignalKick, layered on top of
-- Migrations/020_account_session_claim_race_fix.sql's already-shipped @AttemptNumber parameter and split
-- ConflictLogin branches (never edited in place -- DbMigrator journals every script by content hash and
-- refuses a changed re-apply).
--
-- Gap (behavior contract: accountsessions-transition-race-safety, severity Major -- a distinct finding from
-- 020's Blocker-severity concurrent-claim race): a GameServer/shard that crashes without running
-- GameConnectionHost's own teardown path (MarkTearingDownAsync + ClearIfOwnerAsync) leaves its account's
-- runtime.AccountSessions row stuck at ServerKind=1/SessionState=0, with a LastRefreshedUtc that stops
-- advancing because the only thing that would refresh it -- AccountSessionKickPollHost, on that same dead
-- process -- died with it. A legitimate reconnect during that window previously always hit this procedure's
-- final ELSE branch, which unconditionally set KickRequested=1 and returned Outcome=2 (ConflictGameKicked) --
-- collapsed by LoginService into the same generic "already connected, retry later" refusal as a genuinely
-- live conflict, with nothing left alive anywhere to ever act on that kick flag. The only backstop was
-- AccountSessionReapHost's 6-minute-staleness/60-second-poll sweep: worst case ~6-7 minutes of false
-- "already connected" refusal for an otherwise fully legitimate reconnect.
--
-- Fix: the final ELSE branch (Game-side row, steady state, i.e. SessionState = 0) now cross-checks
-- runtime.GameServerDirectory's LastHeartbeatUtc for the row's own ShardId. This deliberately uses a WIDER
-- 60-second cutoff than usp_GameServer_GetDirectory/usp_CharacterShardLocation_FindByCharacterId's 15-second
-- one: those two only affect read-side routing decisions (worst case, a transient false negative just makes a
-- shard briefly invisible to new traffic), whereas this branch takes a destructive, non-reversible action
-- (deleting and reassigning another account's live session ownership). GameServerDirectoryHeartbeat's default
-- 5-second interval means a 15s cutoff tolerates only 2 consecutive missed heartbeats before treating a shard
-- as dead -- a transient SQL Server hiccup, GC pause, or container CPU throttle can plausibly cause that many
-- misses on an otherwise-healthy shard, which would silently steal a genuinely-still-playing account's session
-- out from under it with no reconciliation path (the original session's own kick-poll is scoped to its own
-- ShardId/ServerKind and would never notice the row moved). 60 seconds (12 missed heartbeats) makes that false
-- positive implausible while still resolving a truly crashed shard's lockout in roughly a tenth of
-- AccountSessionReapHost's 6-7 minute fallback window. If that shard's directory row is missing entirely
-- (already aged out, or proactively evicted by usp_GameServer_MarkUnreachable) or its heartbeat is older than
-- this threshold, the shard is provably dead: the row is fast-cleared and the new claim registered in its
-- place immediately, exactly like the existing stale-Login-row branch, instead of merely flagging a kick
-- nothing will ever service. The new outcome value 4 (ReclaimedDeadShard, added to AccountSessionClaimOutcome)
-- tells LoginService to log this distinctly and otherwise proceed exactly like Registered. A row on a
-- still-heartbeating shard keeps today's behavior unchanged (KickRequested=1, ConflictGameKicked).
--
-- Deliberately out of scope (per this fix's own behavior contract, which explicitly declines to assume an
-- answer here): the SessionState=1 (mid-teardown, Outcome 3/ConflictTearingDown) branch is left untouched.
-- Whether a row correctly mid-teardown on a still-alive shard should ever be treated as fast-clearable, or
-- whether it should share this same liveness check, is a separate, unresolved design question -- not an
-- oversight.
CREATE OR ALTER PROCEDURE runtime.usp_AccountSession_ClaimOrSignalKick
    @AccountId       INT,
    @NewSessionToken UNIQUEIDENTIFIER,
    @AttemptNumber   TINYINT NOT NULL
WITH NATIVE_COMPILATION, SCHEMABINDING
AS
BEGIN ATOMIC
WITH (TRANSACTION ISOLATION LEVEL = SNAPSHOT, LANGUAGE = N'us_english')
    DECLARE @ServerKind TINYINT, @ShardId TINYINT, @SessionState TINYINT, @ShardHeartbeatUtc DATETIME2(3) = NULL;

    SELECT @ServerKind = ServerKind, @ShardId = ShardId, @SessionState = SessionState
    FROM runtime.AccountSessions
    WHERE AccountId = @AccountId;

    -- Read unconditionally (a single cheap hash-indexed lookup on a memory-optimized table) rather than
    -- nesting this SELECT inside the final ELSE branch below -- keeps every DECLARE/top-level SELECT at the
    -- same level as the rest of this procedure's own style. Its result is only ever consulted in that branch;
    -- @ShardId being NULL (Login-side row) or the row not existing yet simply leaves this NULL, harmlessly.
    SELECT @ShardHeartbeatUtc = LastHeartbeatUtc
    FROM runtime.GameServerDirectory
    WHERE ShardId = @ShardId;

    IF @ServerKind IS NULL
    BEGIN
        INSERT INTO runtime.AccountSessions
            (AccountId, ServerKind, ShardId, SessionToken, SessionState, KickRequested, ConnectedAtUtc,
             LastRefreshedUtc)
        VALUES (@AccountId, 0, NULL, @NewSessionToken, 0, 0, SYSUTCDATETIME(), SYSUTCDATETIME());

        SELECT CAST(0 AS TINYINT) AS Outcome, CAST(NULL AS TINYINT) AS PreviousShardId;
    END
    ELSE IF @SessionState = 1
    BEGIN
        SELECT CAST(3 AS TINYINT) AS Outcome, CAST(NULL AS TINYINT) AS PreviousShardId;
    END
    ELSE IF @ServerKind = 0 AND @AttemptNumber = 1
    BEGIN
        -- First attempt seeing an existing Login-side row for this account: a genuinely stale/abandoned
        -- session (no write conflict preceded this read), safe to clear so the caller can proceed once
        -- retried -- self-heals a crashed login process exactly as before this fix.
        DELETE FROM runtime.AccountSessions
        WHERE AccountId = @AccountId;

        SELECT CAST(1 AS TINYINT) AS Outcome, CAST(NULL AS TINYINT) AS PreviousShardId;
    END
    ELSE IF @ServerKind = 0
    BEGIN
        -- @AttemptNumber > 1: this call only exists because our own first attempt's INSERT for this exact
        -- @AccountId already lost a write-write conflict against a concurrent transaction, so the row found
        -- here is provably that concurrent winner's just-committed row, not a stale one -- leave it
        -- untouched. Still report ConflictLogin so the losing caller is refused exactly as before.
        SELECT CAST(1 AS TINYINT) AS Outcome, CAST(NULL AS TINYINT) AS PreviousShardId;
    END
    ELSE IF @ShardHeartbeatUtc IS NULL OR @ShardHeartbeatUtc <= DATEADD(SECOND, -60, SYSUTCDATETIME())
    BEGIN
        -- Game-side row (ServerKind = 1, SessionState = 0) whose owning shard has no directory row at all, or
        -- one whose heartbeat is older than this branch's own wider 60-second threshold (see this procedure's
        -- header comment for why it deliberately differs from usp_GameServer_GetDirectory/
        -- usp_CharacterShardLocation_FindByCharacterId's 15-second one): the shard is provably dead, so
        -- fast-clear and reclaim immediately instead of leaving the row for AccountSessionReapHost's 6-minute
        -- staleness sweep to eventually notice.
        DELETE FROM runtime.AccountSessions
        WHERE AccountId = @AccountId;

        INSERT INTO runtime.AccountSessions
            (AccountId, ServerKind, ShardId, SessionToken, SessionState, KickRequested, ConnectedAtUtc,
             LastRefreshedUtc)
        VALUES (@AccountId, 0, NULL, @NewSessionToken, 0, 0, SYSUTCDATETIME(), SYSUTCDATETIME());

        SELECT CAST(4 AS TINYINT) AS Outcome, @ShardId AS PreviousShardId;
    END
    ELSE
    BEGIN
        -- Shard is still heartbeating within the 15s window: unchanged behavior, flag for the (presumably
        -- live) owning process's own kick-poll to service.
        UPDATE runtime.AccountSessions
        SET KickRequested = 1
        WHERE AccountId = @AccountId;

        SELECT CAST(2 AS TINYINT) AS Outcome, @ShardId AS PreviousShardId;
    END
END;
GO
