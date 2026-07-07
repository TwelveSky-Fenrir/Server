-- The one call every successful credential check makes: this table is the sole cross-process authority for
-- "does this account already have a session, and where". The same atomic block also performs the correct
-- side effect (clear a stale Login row, flag a Game row for kick, or refuse outright), so two near-simultaneous
-- logins for the same account can't both win -- the loser gets a native-compiled write-write conflict from
-- SQL Server itself (error 41302, or a dependency failure 41305/41325) at commit, not silently-wrong data.
-- AccountSessionRepository's wrapper around this call catches those specific SqlException.Number values and
-- retries a small bounded number of times.
--
-- Outcome: 0 = Registered (no prior row), 1 = ConflictLogin (stale Login row cleared, caller evicts any
-- local socket), 2 = ConflictGameKicked (live Game row flagged, PreviousShardId tells the caller which shard
-- owns it), 3 = ConflictTearingDown (row mid-teardown, no change made).
CREATE PROCEDURE runtime.usp_AccountSession_ClaimOrSignalKick @AccountId INT,
                                                              @NewSessionToken UNIQUEIDENTIFIER
    WITH NATIVE_COMPILATION , SCHEMABINDING
AS
BEGIN
    ATOMIC
    WITH (TRANSACTION ISOLATION LEVEL = SNAPSHOT, LANGUAGE = N'us_english')
    DECLARE
        @ServerKind TINYINT, @ShardId TINYINT, @SessionState TINYINT;

    SELECT @ServerKind = ServerKind, @ShardId = ShardId, @SessionState = SessionState
    FROM runtime.AccountSessions
    WHERE AccountId = @AccountId;

    IF
        @ServerKind IS NULL
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
            IF @ServerKind = 0
                BEGIN
                    DELETE
                    FROM runtime.AccountSessions
                    WHERE AccountId = @AccountId;

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
