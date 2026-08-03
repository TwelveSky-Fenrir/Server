CREATE PROCEDURE game.usp_Character_AdjustStoreMoney @CharacterId INT,
                                                     @DeltaMoney BIGINT,
                                                     @DeltaStoreMoney BIGINT,
                                                     @AuditAccountId INT = NULL,
                                                     @AuditEventCode SMALLINT = NULL,
                                                     @AuditQuantity INT = NULL
AS
BEGIN
    SET
        NOCOUNT ON;
    SET
        XACT_ABORT ON;

    BEGIN TRANSACTION;

    UPDATE game.Characters
    SET Money        = Money + @DeltaMoney,
        StoreMoney   = StoreMoney + @DeltaStoreMoney,
        UpdatedAtUtc = SYSUTCDATETIME()
    WHERE CharacterId = @CharacterId
      AND Money + @DeltaMoney BETWEEN 0 AND 2000000000
      AND StoreMoney + @DeltaStoreMoney BETWEEN 0 AND 2000000000;

    IF
        @@ROWCOUNT = 0
        THROW 50337, N'Unknown character or insufficient balance for this Money/StoreMoney adjustment.', 1;

    IF @AuditEventCode IS NOT NULL
        EXEC game.usp_EventLog_Insert
             @EventCode = @AuditEventCode,
             @Category = 19,
             @ActorAccountId = @AuditAccountId,
             @ActorCharacterId = @CharacterId,
             @DeltaMoney = @DeltaMoney,
             @Quantity = @AuditQuantity,
             @Outcome = 1;

    COMMIT TRANSACTION;
END;
