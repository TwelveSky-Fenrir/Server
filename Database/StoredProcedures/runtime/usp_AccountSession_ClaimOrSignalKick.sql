CREATE PROCEDURE runtime.usp_AccountSession_ClaimOrSignalKick @AccountId INT,
                                                              @NewSessionToken UNIQUEIDENTIFIER,
                                                              @AttemptNumber TINYINT NOT NULL
                                                                                     WITH NATIVE_COMPILATION, SCHEMABINDING
    AS
BEGIN
    ATOMIC
    WITH (TRANSACTION ISOLATION LEVEL = SNAPSHOT, LANGUAGE = N'us_english')
    DECLARE @ServerKind TINYINT, @ShardId TINYINT, @SessionState TINYINT, @CurrentSessionToken UNIQUEIDENTIFIER,
        @ShardHeartbeatUtc DATETIME2(3) = NULL;

    SELECT @ServerKind = ServerKind,
           @ShardId = ShardId,
           @SessionState = SessionState,
           @CurrentSessionToken = SessionToken
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
            IF @ServerKind = 0
                BEGIN
                    SELECT CAST(1 AS TINYINT) AS Outcome, CAST(NULL AS TINYINT) AS PreviousShardId;
                END
            ELSE
                IF @ShardHeartbeatUtc IS NULL OR @ShardHeartbeatUtc <= DATEADD(SECOND, -60, SYSUTCDATETIME())
                    BEGIN
                        UPDATE runtime.AccountSessions
                        SET ServerKind       = 0,
                            ShardId          = NULL,
                            SessionToken     = @NewSessionToken,
                            SessionState     = 0,
                            KickRequested    = 0,
                            ConnectedAtUtc   = SYSUTCDATETIME(),
                            LastRefreshedUtc = SYSUTCDATETIME()
                        WHERE AccountId = @AccountId
                          AND ServerKind = 1
                          AND ShardId = @ShardId
                          AND SessionToken = @CurrentSessionToken
                          AND SessionState = 0;

                        IF @@ROWCOUNT = 1
                            SELECT CAST(4 AS TINYINT) AS Outcome, @ShardId AS PreviousShardId;
                        ELSE
                            SELECT CAST(2 AS TINYINT) AS Outcome, CAST(NULL AS TINYINT) AS PreviousShardId;
                    END
                ELSE
                    BEGIN
                        UPDATE runtime.AccountSessions
                        SET KickRequested = 1
                        WHERE AccountId = @AccountId
                          AND ServerKind = 1
                          AND ShardId = @ShardId
                          AND SessionToken = @CurrentSessionToken
                          AND SessionState = 0;

                        IF @@ROWCOUNT = 1
                            SELECT CAST(2 AS TINYINT) AS Outcome, @ShardId AS PreviousShardId;
                        ELSE
                            SELECT CAST(2 AS TINYINT) AS Outcome, CAST(NULL AS TINYINT) AS PreviousShardId;
                    END
END;
