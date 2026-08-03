
DROP PROCEDURE IF EXISTS runtime.usp_SessionTicket_Consume;
DROP PROCEDURE IF EXISTS runtime.usp_SessionTicket_Create;
DROP PROCEDURE IF EXISTS runtime.usp_SessionTicket_Purge;
DROP TABLE IF EXISTS runtime.SessionTickets;
GO

CREATE TABLE runtime.SessionTickets
(
    AccountId    INT              NOT NULL,
    CharacterId  INT              NOT NULL,
    ShardId      TINYINT          NOT NULL, 
    TargetMapId  SMALLINT         NOT NULL, 
    ExpiresAtUtc DATETIME2(3)     NOT NULL,
    SessionToken UNIQUEIDENTIFIER NOT NULL, 
    AccountGrade SMALLINT         NOT NULL, 
    CONSTRAINT PK_SessionTickets PRIMARY KEY NONCLUSTERED HASH (AccountId)
        WITH (BUCKET_COUNT = 1024),
    INDEX IX_SessionTickets_ExpiresAtUtc NONCLUSTERED (ExpiresAtUtc)
)
    WITH (MEMORY_OPTIMIZED = ON, DURABILITY = SCHEMA_ONLY);
GO

CREATE PROCEDURE runtime.usp_SessionTicket_Create @AccountId INT,
                                                  @CharacterId INT,
                                                  @ShardId TINYINT,
                                                  @TtlSeconds INT,
                                                  @SessionToken UNIQUEIDENTIFIER,
                                                  @AccountGrade SMALLINT,
                                                  @TargetMapId SMALLINT
    WITH NATIVE_COMPILATION , SCHEMABINDING
AS
BEGIN
    ATOMIC
    WITH (TRANSACTION ISOLATION LEVEL = SNAPSHOT, LANGUAGE = N'us_english')
    DELETE
    FROM runtime.SessionTickets
    WHERE AccountId = @AccountId;

    INSERT INTO runtime.SessionTickets (AccountId, CharacterId, ShardId, TargetMapId, ExpiresAtUtc, SessionToken,
                                        AccountGrade)
    VALUES (@AccountId, @CharacterId, @ShardId, @TargetMapId, DATEADD(SECOND, @TtlSeconds, SYSUTCDATETIME()),
            @SessionToken, @AccountGrade);
END;
GO

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
GO

CREATE PROCEDURE runtime.usp_SessionTicket_Purge
    WITH NATIVE_COMPILATION ,
        SCHEMABINDING
AS
BEGIN
    ATOMIC
    WITH (TRANSACTION ISOLATION LEVEL = SNAPSHOT, LANGUAGE = N'us_english')
    DELETE
    FROM runtime.SessionTickets
    WHERE ExpiresAtUtc <= SYSUTCDATETIME();
END;
GO
