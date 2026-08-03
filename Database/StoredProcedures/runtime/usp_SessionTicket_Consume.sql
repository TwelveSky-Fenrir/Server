CREATE PROCEDURE runtime.usp_SessionTicket_Consume @AccountId INT
    WITH NATIVE_COMPILATION , SCHEMABINDING
AS
BEGIN
    ATOMIC
    WITH (TRANSACTION ISOLATION LEVEL = SNAPSHOT, LANGUAGE = N'us_english')
    DECLARE
        @CharacterId INT, @ShardId TINYINT, @Exp DATETIME2(3), @SessionToken UNIQUEIDENTIFIER,
        @AccountGrade SMALLINT, @TargetMapId SMALLINT;

    SELECT @CharacterId = CharacterId,
           @ShardId = ShardId,
           @Exp = ExpiresAtUtc,
           @SessionToken = SessionToken,
           @AccountGrade = AccountGrade,
           @TargetMapId = TargetMapId
    FROM runtime.SessionTickets
    WHERE AccountId = @AccountId;

    DELETE
    FROM runtime.SessionTickets
    WHERE AccountId = @AccountId;

    IF @Exp IS NOT NULL AND @Exp > SYSUTCDATETIME()
        SELECT @CharacterId  AS CharacterId,
               @ShardId      AS ShardId,
               @SessionToken AS SessionToken,
               @AccountGrade AS AccountGrade,
               @TargetMapId  AS TargetMapId;
END;
