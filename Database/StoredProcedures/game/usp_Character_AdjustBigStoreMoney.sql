CREATE PROCEDURE game.usp_Character_AdjustBigStoreMoney @CharacterId INT,
                                                        @DeltaBigMoney INT,
                                                        @DeltaBigStoreMoney INT,
                                                        @AuditEventCode SMALLINT = NULL,
                                                        @AuditFromDelta BIGINT = NULL,
                                                        @AuditToDelta BIGINT = NULL
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    BEGIN TRANSACTION;

    UPDATE game.Characters
    SET BigMoney      = BigMoney + @DeltaBigMoney,
        BigStoreMoney = BigStoreMoney + @DeltaBigStoreMoney,
        UpdatedAtUtc  = SYSUTCDATETIME()
    WHERE CharacterId = @CharacterId
      AND BigMoney + @DeltaBigMoney BETWEEN 0 AND 999
      AND BigStoreMoney + @DeltaBigStoreMoney BETWEEN 0 AND 999;

    IF @@ROWCOUNT = 0
        THROW 50349, N'Unknown character or insufficient balance for this BigMoney/BigStoreMoney adjustment.', 1;

    IF @AuditEventCode IS NOT NULL
        EXEC game.usp_EventLog_Insert
             @EventCode = @AuditEventCode,
             @Category = 25,
             @ActorCharacterId = @CharacterId,
             @DeltaMoney = @AuditFromDelta,
             @DeltaBigMoney = @AuditToDelta,
             @Outcome = 1;

    COMMIT TRANSACTION;
END;
