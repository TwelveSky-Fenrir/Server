-- Atomic transfer between a character's wallet (Money) and its own Store/coffre money pool (StoreMoney) --
-- CZ_PROCESS_DATA_SEND tSort 226 (deposit, DeltaMoney negative/DeltaStoreMoney positive) / 227 (withdraw,
-- the reverse). Both columns live on the same game.Characters row, so the money-adjustment UPDATE alone
-- would be atomic without an explicit transaction -- but @AuditEventCode optionally nests the
-- StoreSlotMoney audit row (EventLogCategory.StoreSlotMoney = 19) as a second write in the same
-- transaction, following usp_CharacterTrade_Execute's own precedent (transaction-composition-audit
-- finding), so an explicit BEGIN/COMMIT TRANSACTION is now required once there is more than one write
-- statement. Caller omits @AuditEventCode (stays NULL) to skip logging.
-- Réf. C++ : Server/ts25zone/S04_MyWork05.cpp:2903-2969 (ProcessForInventoryMoneyToStoreMoney/
-- ProcessForStoreMoneyToInventoryMoney) ; Server/Header/Protocol/DEFINE.h:365 (MAX_NUMBER_SIZE = 2,000,000,000,
-- the cap both pools share).
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

    -- Guarded UPDATE closes a TOCTOU: two concurrent transfers must never jointly breach either cap or drive
    -- either pool negative.
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
             @Category = 19, -- EventLogCategory.StoreSlotMoney
             @ActorAccountId = @AuditAccountId,
             @ActorCharacterId = @CharacterId,
             @DeltaMoney = @DeltaMoney,
             @Quantity = @AuditQuantity,
             @Outcome = 1;

    COMMIT TRANSACTION;
END;
