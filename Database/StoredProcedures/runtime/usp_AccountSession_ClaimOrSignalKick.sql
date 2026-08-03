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
                    DELETE
                    FROM runtime.AccountSessions
                    WHERE AccountId = @AccountId;

                    SELECT CAST(1 AS TINYINT) AS Outcome, CAST(NULL AS TINYINT) AS PreviousShardId;
                END
            ELSE
                IF @ServerKind = 0
                    BEGIN
                        SELECT CAST(1 AS TINYINT) AS Outcome, CAST(NULL AS TINYINT) AS PreviousShardId;
                    END
                ELSE
                    IF @ShardHeartbeatUtc IS NULL OR @ShardHeartbeatUtc <= DATEADD(SECOND, -60, SYSUTCDATETIME())
                        BEGIN
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
                            UPDATE runtime.AccountSessions
                            SET KickRequested = 1
                            WHERE AccountId = @AccountId;

                            SELECT CAST(2 AS TINYINT) AS Outcome, @ShardId AS PreviousShardId;
                        END
END;
