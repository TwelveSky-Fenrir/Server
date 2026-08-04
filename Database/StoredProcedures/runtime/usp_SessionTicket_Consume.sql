CREATE PROCEDURE runtime.usp_SessionTicket_Consume @CapabilityHash BINARY(32),
                                                   @ExpectedShardId TINYINT,
                                                   @ExpectedTargetMapId SMALLINT,
                                                   @SourceIpPrefix VARCHAR(45)
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    DECLARE
        @AccountId INT, @CharacterId INT, @ShardId TINYINT, @Exp DATETIME2(3), @SessionToken UNIQUEIDENTIFIER,
        @AccountGrade SMALLINT, @TargetMapId SMALLINT, @BoundIpPrefix VARCHAR(45);

    IF @ExpectedShardId = 0
        OR @ExpectedTargetMapId <= 0
        OR @SourceIpPrefix IS NULL
        OR @SourceIpPrefix = ''
        RETURN;

    BEGIN TRANSACTION;

    IF NOT EXISTS
        (SELECT 1
         FROM admin.ShardMapAssignments
         WITH (UPDLOCK, HOLDLOCK)
         WHERE ShardId = @ExpectedShardId
           AND MapId = @ExpectedTargetMapId)
        GOTO Rejected;

    SELECT @AccountId = AccountId,
           @CharacterId = CharacterId,
           @ShardId = ShardId,
           @Exp = ExpiresAtUtc,
           @SessionToken = SessionToken,
           @AccountGrade = AccountGrade,
           @TargetMapId = TargetMapId,
           @BoundIpPrefix = SourceIpPrefix
    FROM runtime.SessionTickets WITH (SNAPSHOT)
    WHERE CapabilityHash = @CapabilityHash;

    IF @AccountId IS NULL
        GOTO Rejected;

    IF @Exp <= SYSUTCDATETIME()
        OR @ShardId <> @ExpectedShardId
        OR @TargetMapId <> @ExpectedTargetMapId
        OR @SourceIpPrefix <> @BoundIpPrefix
        GOTO Rejected;

    IF EXISTS
        (SELECT 1
         FROM admin.Bans
         WITH (UPDLOCK, HOLDLOCK)
         WHERE (AccountId = @AccountId OR CharacterId = @CharacterId)
           AND (ExpiresAtUtc IS NULL OR ExpiresAtUtc > SYSUTCDATETIME()))
        GOTO Rejected;

    UPDATE runtime.AccountSessions WITH (SNAPSHOT)
    SET LastRefreshedUtc = SYSUTCDATETIME()
    WHERE AccountId = @AccountId
      AND SessionToken = @SessionToken
      AND SessionState = 0
      AND KickRequested = 0;

    IF @@ROWCOUNT <> 1
        GOTO Rejected;

    DELETE
    FROM runtime.SessionTickets WITH (SNAPSHOT)
    WHERE CapabilityHash = @CapabilityHash
      AND AccountId = @AccountId
      AND CharacterId = @CharacterId
      AND ShardId = @ExpectedShardId
      AND TargetMapId = @ExpectedTargetMapId
      AND SessionToken = @SessionToken
      AND AccountGrade = @AccountGrade
      AND SourceIpPrefix = @SourceIpPrefix
      AND ExpiresAtUtc > SYSUTCDATETIME();

    IF @@ROWCOUNT <> 1
        GOTO Rejected;

    COMMIT TRANSACTION;

    SELECT @AccountId    AS AccountId,
           @CharacterId  AS CharacterId,
           @ShardId      AS ShardId,
           @SessionToken AS SessionToken,
           @AccountGrade AS AccountGrade,
           @TargetMapId  AS TargetMapId;

    RETURN;

    Rejected:
    ROLLBACK TRANSACTION;
END;
