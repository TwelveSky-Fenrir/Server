-- Additive script: usp_AccountVault_TransferBigMoneyWithCharacter.sql stays unchanged (DbMigrator journals
-- it by SHA-256 and would refuse to reapply it if edited). CREATE OR ALTER on the same procedure name, same
-- pattern as usp_AccountVault_TransferMoneyWithCharacter_VaultCreateRaceGuard.sql (that file's own header
-- carries the full race explanation this one shares -- identical AccountVault bootstrap shape, just the
-- BigMoney pair of columns/error codes/audit category instead of the Money pair).
CREATE OR ALTER PROCEDURE game.usp_AccountVault_TransferBigMoneyWithCharacter @CharacterId INT,
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
        BEGIN
            BEGIN TRY
                INSERT INTO game.AccountVault (AccountId) VALUES (@AccountId);
            END TRY
            BEGIN CATCH
                IF ERROR_NUMBER() NOT IN (2627, 2601)
                    THROW;

                ROLLBACK TRANSACTION;

                BEGIN TRANSACTION;

                UPDATE game.Characters
                SET BigMoney     = BigMoney + @DeltaCharacterBigMoney,
                    UpdatedAtUtc = SYSUTCDATETIME()
                WHERE CharacterId = @CharacterId
                  AND BigMoney + @DeltaCharacterBigMoney BETWEEN 0 AND 999;

                IF @@ROWCOUNT = 0
                    BEGIN
                        ROLLBACK TRANSACTION;
                        THROW 50353, N'Unknown character or insufficient/over-cap BigMoney balance for this vault transfer (retry).', 1;
                    END;

                UPDATE game.AccountVault
                SET BigMoney     = BigMoney + @DeltaVaultBigMoney,
                    UpdatedAtUtc = SYSUTCDATETIME()
                WHERE AccountId = @AccountId
                  AND BigMoney + @DeltaVaultBigMoney BETWEEN 0 AND 999;

                IF @@ROWCOUNT = 0
                    BEGIN
                        ROLLBACK TRANSACTION;
                        THROW 50354, N'Insufficient/over-cap account vault BigMoney balance for this transfer (retry).', 1;
                    END;

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

                RETURN;
            END CATCH;
        END;

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
