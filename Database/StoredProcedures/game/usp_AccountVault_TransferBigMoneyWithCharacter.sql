-- Atomic transfer between a character's on-hand BigMoney (game.Characters.BigMoney) and its account's shared
-- vault BigMoney pool (game.AccountVault.BigMoney) -- CZ_PROCESS_DATA_SEND tSort 242 (deposit,
-- DeltaCharacterBigMoney negative/DeltaVaultBigMoney positive) / 245 (withdraw, the reverse). Auto-creates the
-- AccountVault row on first use, same posture as usp_AccountVault_TransferMoneyWithCharacter.
-- Réf. C++ : Server/ts25zone/S04_MyWork05.cpp:3736-3769 (ProcessForInventoryMoneyTo1BSaveMoney) ; :3771-3804
-- (ProcessFor1BSaveMoneyToInventoryMoney) ; Server/Header/Protocol/DEFINE.h:367 (MAX_NUMBER_SIZE2 = 999).
--
-- @AuditEventCode etc. optionally nest the BigMoneyConversion audit row (EventLogCategory.BigMoneyConversion
-- = 25) into this same already-open transaction, following usp_CharacterTrade_Execute's own precedent
-- (transaction-composition-audit finding). @AuditFromDelta/@AuditToDelta mirror
-- IEventLogRepository.LogBigMoneyConversionAsync's two-independent-longs shape (source-pool/destination-pool
-- delta, direction-dependent -- not necessarily @DeltaCharacterBigMoney/@DeltaVaultBigMoney in that order).
CREATE PROCEDURE game.usp_AccountVault_TransferBigMoneyWithCharacter @CharacterId INT,
                                                                     @DeltaCharacterBigMoney INT,
                                                                     @AccountId INT,
                                                                     @DeltaVaultBigMoney INT,
                                                                     @AuditEventCode SMALLINT = NULL,
                                                                     @AuditFromDelta BIGINT = NULL,
                                                                     @AuditToDelta BIGINT = NULL
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    BEGIN TRANSACTION;

    IF NOT EXISTS (SELECT 1 FROM game.AccountVault WHERE AccountId = @AccountId)
        INSERT INTO game.AccountVault (AccountId) VALUES (@AccountId);

    UPDATE game.Characters
    SET BigMoney     = BigMoney + @DeltaCharacterBigMoney,
        UpdatedAtUtc = SYSUTCDATETIME()
    WHERE CharacterId = @CharacterId
      AND BigMoney + @DeltaCharacterBigMoney BETWEEN 0 AND 999;

    IF @@ROWCOUNT = 0
        THROW 50353, N'Unknown character or insufficient/over-cap BigMoney balance for this vault transfer.', 1;

    UPDATE game.AccountVault
    SET BigMoney     = BigMoney + @DeltaVaultBigMoney,
        UpdatedAtUtc = SYSUTCDATETIME()
    WHERE AccountId = @AccountId
      AND BigMoney + @DeltaVaultBigMoney BETWEEN 0 AND 999;

    IF @@ROWCOUNT = 0
        THROW 50354, N'Insufficient/over-cap account vault BigMoney balance for this transfer.', 1;

    IF @AuditEventCode IS NOT NULL
        EXEC game.usp_EventLog_Insert
             @EventCode = @AuditEventCode,
             @Category = 25, -- EventLogCategory.BigMoneyConversion
             @ActorAccountId = @AccountId,
             @ActorCharacterId = @CharacterId,
             @DeltaMoney = @AuditFromDelta,
             @DeltaBigMoney = @AuditToDelta,
             @Outcome = 1;

    COMMIT TRANSACTION;
END;
