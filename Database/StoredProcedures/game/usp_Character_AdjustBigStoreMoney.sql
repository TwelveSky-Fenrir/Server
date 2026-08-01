-- Atomic transfer between a character's on-hand BigMoney (game.Characters.BigMoney) and its own Store/coffre
-- BigMoney pool (game.Characters.BigStoreMoney) -- CZ_PROCESS_DATA_SEND tSort 241 (Inventory->Store,
-- DeltaBigMoney negative/DeltaBigStoreMoney positive) / 244 (Store->Inventory, the reverse). Both columns
-- already exist on game.Characters (Tables/game/Characters.sql) since the initial schema, so this is a plain
-- StoredProcedures/ script -- unlike the pet-bag/Save-BigMoney additions in this same wave, no new column is
-- introduced here. Same "single guarded UPDATE, no explicit transaction needed" shape as
-- usp_Character_AdjustStoreMoney.
-- Réf. C++ : Server/ts25zone/S04_MyWork05.cpp:3666-3699 (ProcessForInventoryMoneyTo1BStoreMoney) ; :3701-3734
-- (ProcessFor1BStoreMoneyToInventoryMoney) ; Server/Header/Protocol/DEFINE.h:367 (MAX_NUMBER_SIZE2 = 999, the
-- BigMoney-family cap both pools share).
--
-- @AuditEventCode etc. optionally nest the BigMoneyConversion audit row (EventLogCategory.BigMoneyConversion
-- = 25) into this same transaction, following usp_CharacterTrade_Execute's own precedent
-- (transaction-composition-audit finding). @AuditFromDelta/@AuditToDelta are the caller-computed
-- source-pool/destination-pool deltas (not necessarily @DeltaBigMoney/@DeltaBigStoreMoney in that order --
-- direction depends on deposit vs. withdraw), matching IEventLogRepository.LogBigMoneyConversionAsync's own
-- two-independent-longs shape. This is now a 2-write procedure, so the previously-implicit single-UPDATE
-- atomicity needs an explicit transaction.
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

    -- Guarded UPDATE closes a TOCTOU: two concurrent transfers must never jointly breach either 999 cap or
    -- drive either pool negative.
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
             @Category = 25, -- EventLogCategory.BigMoneyConversion
             @ActorCharacterId = @CharacterId,
             @DeltaMoney = @AuditFromDelta,
             @DeltaBigMoney = @AuditToDelta,
             @Outcome = 1;

    COMMIT TRANSACTION;
END;
