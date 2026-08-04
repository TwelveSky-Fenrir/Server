CREATE PROCEDURE runtime.usp_SessionTicket_Create @AccountId INT,
                                                  @CharacterId INT,
                                                  @ShardId TINYINT,
                                                  @TtlSeconds INT,
                                                  @SessionToken UNIQUEIDENTIFIER,
                                                  @AccountGrade SMALLINT,
                                                  @TargetMapId SMALLINT,
                                                  @CapabilityHash BINARY(32),
                                                  @SourceIpPrefix VARCHAR(45)
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    IF @AccountId <= 0
        OR @CharacterId <= 0
        OR @ShardId = 0
        OR @TargetMapId <= 0
        OR @TtlSeconds <= 0
        OR @AccountGrade < 0
        OR @SourceIpPrefix IS NULL
        OR @SourceIpPrefix = ''
        BEGIN
            SELECT CAST(0 AS BIT) AS Accepted;
            RETURN;
        END

    BEGIN TRANSACTION;

    IF NOT EXISTS
        (SELECT 1
         FROM admin.ShardMapAssignments
         WITH (UPDLOCK, HOLDLOCK)
         WHERE ShardId = @ShardId
           AND MapId = @TargetMapId)
        BEGIN
            ROLLBACK TRANSACTION;
            SELECT CAST(0 AS BIT) AS Accepted;
            RETURN;
        END

    IF NOT EXISTS
        (SELECT 1
         FROM game.Characters
         WITH (UPDLOCK, HOLDLOCK)
         WHERE CharacterId = @CharacterId
           AND AccountId = @AccountId)
        BEGIN
            ROLLBACK TRANSACTION;
            SELECT CAST(0 AS BIT) AS Accepted;
            RETURN;
        END

    UPDATE runtime.AccountSessions WITH (SNAPSHOT)
    SET LastRefreshedUtc = SYSUTCDATETIME()
    WHERE AccountId = @AccountId
      AND SessionToken = @SessionToken
      AND SessionState = 0
      AND KickRequested = 0;

    IF @@ROWCOUNT <> 1
        BEGIN
            ROLLBACK TRANSACTION;
            SELECT CAST(0 AS BIT) AS Accepted;
            RETURN;
        END

    DELETE
    FROM runtime.SessionTickets WITH (SNAPSHOT)
    WHERE AccountId = @AccountId;

    INSERT INTO runtime.SessionTickets (CapabilityHash, AccountId, CharacterId, ShardId, TargetMapId, ExpiresAtUtc,
                                        SessionToken, AccountGrade, SourceIpPrefix)
    VALUES (@CapabilityHash, @AccountId, @CharacterId, @ShardId, @TargetMapId,
            DATEADD(SECOND, @TtlSeconds, SYSUTCDATETIME()), @SessionToken, @AccountGrade, @SourceIpPrefix);

    COMMIT TRANSACTION;

    SELECT CAST(1 AS BIT) AS Accepted;
END;
