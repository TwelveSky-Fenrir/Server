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
