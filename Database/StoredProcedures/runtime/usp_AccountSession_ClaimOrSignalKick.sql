-- The one call every successful credential check makes: this table is the sole cross-process authority for
-- "does this account already have a session, and where". The same atomic block also performs the correct
-- side effect (clear a stale Login row, flag a Game row for kick, fast-reclaim a dead shard's row, or refuse
-- outright), so two near-simultaneous logins for the same account can't both win -- the loser gets a
-- native-compiled write-write conflict from SQL Server itself (error 41302, or a dependency failure
-- 41305/41325) at commit, not silently-wrong data. AccountSessionRepository's wrapper around this call
-- catches those specific SqlException.Number values and retries a small bounded number of times, passing its
-- own 1-based attempt number as @AttemptNumber.
--
-- @AttemptNumber distinguishes a first-ever observation of an existing Login-side row (genuinely stale/
-- abandoned, safe to clear) from a retry after this exact call's own first-attempt INSERT already lost a
-- write-write conflict against a concurrent transaction for the SAME @AccountId -- in that case the row found
-- on retry is provably the concurrent winner's just-committed row, not a stale one, and must be left
-- untouched (still reporting ConflictLogin so the losing caller is refused exactly as before).
--
-- The final branch (Game-side row, steady state) cross-checks runtime.GameServerDirectory's LastHeartbeatUtc
-- for the row's own ShardId against a 60-second cutoff -- deliberately wider than
-- usp_GameServer_GetDirectory/usp_CharacterShardLocation_FindByCharacterId's 15-second one, since this branch
-- takes a destructive, non-reversible action (reassigning session ownership) rather than a read-side routing
-- decision. If the shard's directory row is missing entirely or its heartbeat is older than this threshold,
-- the shard is provably dead: the row is fast-cleared and the new claim registered in its place immediately,
-- instead of leaving it for AccountSessionReapHost's 6-minute staleness sweep to eventually notice.
--
-- Outcome: 0 = Registered (no prior row), 1 = ConflictLogin (stale Login row cleared, caller evicts any
-- local socket), 2 = ConflictGameKicked (live Game row flagged, PreviousShardId tells the caller which shard
-- owns it), 3 = ConflictTearingDown (row mid-teardown, no change made), 4 = ReclaimedDeadShard (Game row on a
-- provably dead shard fast-cleared and reclaimed, proceed exactly like Registered).
CREATE PROCEDURE runtime.usp_AccountSession_ClaimOrSignalKick @AccountId INT,
                                                               @NewSessionToken UNIQUEIDENTIFIER,
                                                               @AttemptNumber TINYINT NOT NULL
                                                                                      WITH NATIVE_COMPILATION, SCHEMABINDING
    AS
BEGIN
    ATOMIC
    WITH (TRANSACTION ISOLATION LEVEL = SNAPSHOT, LANGUAGE = N'us_english')
    DECLARE @ServerKind TINYINT, @ShardId TINYINT, @SessionState TINYINT, @ShardHeartbeatUtc DATETIME2(3) = NULL;

    SELECT @ServerKind = ServerKind, @ShardId = ShardId, @SessionState = SessionState
    FROM runtime.AccountSessions
    WHERE AccountId = @AccountId;

    -- Read unconditionally (a single cheap hash-indexed lookup on a memory-optimized table) rather than
    -- nesting this SELECT inside the final ELSE branch below. Its result is only ever consulted in that branch;
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
    ELSE
        IF @SessionState = 1
            BEGIN
                SELECT CAST(3 AS TINYINT) AS Outcome, CAST(NULL AS TINYINT) AS PreviousShardId;
            END
        ELSE
            IF @ServerKind = 0 AND @AttemptNumber = 1
                BEGIN
                    -- First attempt seeing an existing Login-side row for this account: a genuinely stale/abandoned
                    -- session (no write conflict preceded this read), safe to clear so the caller can proceed once
                    -- retried -- self-heals a crashed login process.
                    DELETE
                    FROM runtime.AccountSessions
                    WHERE AccountId = @AccountId;

                    SELECT CAST(1 AS TINYINT) AS Outcome, CAST(NULL AS TINYINT) AS PreviousShardId;
                END
            ELSE
                IF @ServerKind = 0
                    BEGIN
                        -- @AttemptNumber > 1: this call only exists because our own first attempt's INSERT for this exact
                        -- @AccountId already lost a write-write conflict against a concurrent transaction, so the row found
                        -- here is provably that concurrent winner's just-committed row, not a stale one -- leave it
                        -- untouched. Still report ConflictLogin so the losing caller is refused exactly as before.
                        SELECT CAST(1 AS TINYINT) AS Outcome, CAST(NULL AS TINYINT) AS PreviousShardId;
                    END
                ELSE
                    IF @ShardHeartbeatUtc IS NULL OR @ShardHeartbeatUtc <= DATEADD(SECOND, -60, SYSUTCDATETIME())
                        BEGIN
                            -- Game-side row (ServerKind = 1, SessionState = 0) whose owning shard has no directory row at all, or
                            -- one whose heartbeat is older than this branch's own wider 60-second threshold: the shard is
                            -- provably dead, so fast-clear and reclaim immediately instead of leaving the row for
                            -- AccountSessionReapHost's 6-minute staleness sweep to eventually notice.
                            DELETE
                            FROM runtime.AccountSessions
                            WHERE AccountId = @AccountId;

                            INSERT INTO runtime.AccountSessions
                            (AccountId, ServerKind, ShardId, SessionToken, SessionState, KickRequested, ConnectedAtUtc,
                             LastRefreshedUtc)
                            VALUES (@AccountId, 0, NULL, @NewSessionToken, 0, 0, SYSUTCDATETIME(), SYSUTCDATETIME());

                            SELECT CAST(4 AS TINYINT) AS Outcome, @ShardId AS PreviousShardId;
                        END
                    ELSE
                        BEGIN
                            -- Shard is still heartbeating within the 60s window: unchanged behavior, flag for the (presumably
                            -- live) owning process's own kick-poll to service.
                            UPDATE runtime.AccountSessions
                            SET KickRequested = 1
                            WHERE AccountId = @AccountId;

                            SELECT CAST(2 AS TINYINT) AS Outcome, @ShardId AS PreviousShardId;
                        END
END;
