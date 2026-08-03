CREATE OR ALTER PROCEDURE game.usp_AccountVault_TransferMoneyWithCharacter @CharacterId INT,
                                                                           @DeltaCharacterMoney BIGINT,
                                                                           @AccountId INT,
                                                                           @DeltaVaultMoney BIGINT,
                                                                           @AuditEventCode SMALLINT = NULL,
                                                                           @AuditQuantity INT = NULL
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
                SET Money        = Money + @DeltaCharacterMoney,
                    UpdatedAtUtc = SYSUTCDATETIME()
                WHERE CharacterId = @CharacterId
                  AND Money + @DeltaCharacterMoney BETWEEN 0 AND 2000000000;

                IF @@ROWCOUNT = 0
                    BEGIN
                        ROLLBACK TRANSACTION;
                        THROW 50338, N'Unknown character or insufficient wallet balance for this vault transfer (retry).', 1;
                    END;

                UPDATE game.AccountVault
                SET Money        = Money + @DeltaVaultMoney,
                    UpdatedAtUtc = SYSUTCDATETIME()
                WHERE AccountId = @AccountId
                  AND Money + @DeltaVaultMoney BETWEEN 0 AND 2000000000;

                IF @@ROWCOUNT = 0
                    BEGIN
                        ROLLBACK TRANSACTION;
                        THROW 50339, N'Insufficient account vault balance for this transfer (retry).', 1;
                    END;

                IF @AuditEventCode IS NOT NULL
                    EXEC game.usp_EventLog_Insert
                         @EventCode = @AuditEventCode,
                         @Category = 20,
                         @ActorAccountId = @AccountId,
                         @ActorCharacterId = @CharacterId,
                         @DeltaMoney = @DeltaCharacterMoney,
                         @Quantity = @AuditQuantity,
                         @Outcome = 1;

                COMMIT TRANSACTION;

                RETURN;
            END CATCH;
        END;

    UPDATE game.Characters
    SET Money        = Money + @DeltaCharacterMoney,
        UpdatedAtUtc = SYSUTCDATETIME()
    WHERE CharacterId = @CharacterId
      AND Money + @DeltaCharacterMoney BETWEEN 0 AND 2000000000;

    IF @@ROWCOUNT = 0
        THROW 50338, N'Unknown character or insufficient wallet balance for this vault transfer.', 1;

    UPDATE game.AccountVault
    SET Money        = Money + @DeltaVaultMoney,
        UpdatedAtUtc = SYSUTCDATETIME()
    WHERE AccountId = @AccountId
      AND Money + @DeltaVaultMoney BETWEEN 0 AND 2000000000;

    IF @@ROWCOUNT = 0
        THROW 50339, N'Insufficient account vault balance for this transfer.', 1;

    IF @AuditEventCode IS NOT NULL
        EXEC game.usp_EventLog_Insert
             @EventCode = @AuditEventCode,
             @Category = 20,
             @ActorAccountId = @AccountId,
             @ActorCharacterId = @CharacterId,
             @DeltaMoney = @DeltaCharacterMoney,
             @Quantity = @AuditQuantity,
             @Outcome = 1;

    COMMIT TRANSACTION;
END;
