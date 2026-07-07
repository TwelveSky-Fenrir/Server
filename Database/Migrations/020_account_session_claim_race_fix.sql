-- Corrective fix for runtime.usp_AccountSession_ClaimOrSignalKick
-- (StoredProcedures/runtime/usp_AccountSession_ClaimOrSignalKick.sql, never edited in place -- DbMigrator
-- journals every script by content hash and refuses a changed re-apply).
--
-- Bug (behavior contract: accountsessions-transition-race-safety, severity Blocker): when two genuinely
-- concurrent LoginAsync calls for the SAME account both reach this procedure with no pre-existing
-- runtime.AccountSessions row -- the common case, any account not already mid-session -- both read
-- @ServerKind IS NULL and both attempt INSERT. Exactly one commits and returns Outcome = 0 (Registered); the
-- other's INSERT fails at commit with a native-compiled write-write conflict (error 41302, or a dependency
-- failure 41305/41325), which AccountSessionRepository.ClaimOrSignalKickAsync's retry loop catches and
-- retries against a brand-new call to this same procedure (AccountSessionRepository.cs:28-50).
--
-- On that retry, the loser re-reads the row -- but now sees the WINNER's just-committed row
-- (ServerKind = 0) -- which the old `ELSE IF @ServerKind = 0` branch could not distinguish from a
-- genuinely stale/abandoned Login-side row left behind by an unrelated, long-past session: it
-- unconditionally deleted it and reported Outcome = 1 (ConflictLogin), with no compensating re-insert. Net
-- effect: the winner's own call had already returned Outcome = Registered, and LoginService.LoginAsync had
-- already completed successfully for it (local association, character list load, session-lifecycle
-- EventLog row, success result returned to that client) -- but the winner's runtime.AccountSessions row was
-- then silently destroyed by the loser's retry. The winner's later op22 zone-transfer would then call
-- usp_AccountSession_TransitionToGame, find zero matching rows, and be wrongly rejected as
-- ZoneHandshakeOutcome.SessionSuperseded ("a newer login already claimed the session") even though no newer
-- login for the account ever actually completed.
--
-- Fix: the C# caller (AccountSessionRepository.ClaimOrSignalKickAsync) now passes its own 1-based attempt
-- number for this logical claim as a new required @AttemptNumber parameter. @AttemptNumber > 1 is reachable
-- below only after this exact call's own first-attempt INSERT already lost a write-write conflict against a
-- concurrent transaction for the SAME @AccountId (the table's HASH-indexed primary key) -- there is no other
-- way for a retry to happen at all. That makes any Login-side row observed on a retry provably the
-- just-committed row of the concurrent winner from this very race, never a pre-existing stale row: a
-- genuinely stale row would already have been found (and safely cleared) on attempt 1, which never throws
-- and so never retries. The old single `ELSE IF @ServerKind = 0` branch is therefore split in two: attempt 1
-- keeps the original stale-row cleanup (delete then report ConflictLogin, self-healing a crashed login
-- process exactly as before); attempt 2+ reports the same Outcome = 1 (ConflictLogin) -- so the losing caller
-- is refused exactly as before, LoginService.cs's ConflictLogin handling needs no change -- but leaves the
-- row untouched, so the winner's session row survives intact for its later zone-transfer to find.
CREATE OR ALTER PROCEDURE runtime.usp_AccountSession_ClaimOrSignalKick @AccountId INT,
                                                                       @NewSessionToken UNIQUEIDENTIFIER,
                                                                       @AttemptNumber TINYINT NOT NULL
                                                                                              WITH NATIVE_COMPILATION, SCHEMABINDING
    AS
BEGIN
    ATOMIC
    WITH (TRANSACTION ISOLATION LEVEL = SNAPSHOT, LANGUAGE = N'us_english')
    DECLARE @ServerKind TINYINT, @ShardId TINYINT, @SessionState TINYINT;

    SELECT @ServerKind = ServerKind, @ShardId = ShardId, @SessionState = SessionState
    FROM runtime.AccountSessions
    WHERE AccountId = @AccountId;

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
                    -- retried -- self-heals a crashed login process exactly as before this fix.
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
                    BEGIN
                        UPDATE runtime.AccountSessions
                        SET KickRequested = 1
                        WHERE AccountId = @AccountId;

                        SELECT CAST(2 AS TINYINT) AS Outcome, @ShardId AS PreviousShardId;
                    END
END;
GO
