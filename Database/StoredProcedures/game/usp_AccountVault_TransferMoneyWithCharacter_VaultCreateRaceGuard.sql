-- Additive script: usp_AccountVault_TransferMoneyWithCharacter.sql stays unchanged (DbMigrator journals it
-- by SHA-256 and would refuse to reapply it if edited). CREATE OR ALTER on the same procedure name, same
-- pattern as usp_Cash_Credit_FirstCreditRaceGuard.sql (that file's own header carries the full race
-- explanation this one shares).
--
-- AccountVault auto-create race: the IF NOT EXISTS(...) INSERT INTO game.AccountVault bootstrap (PK_AccountVault
-- on AccountId alone -- no bootstrap proc ever pre-creates the row) has no TRY/CATCH, inside this proc's own
-- explicit BEGIN TRANSACTION under XACT_ABORT ON. Two concurrent first-ever vault touches for the SAME
-- @AccountId (e.g. an auto-claimed gift racing a manual deposit -- see usp_Gift_ClaimIntoVault, which shares
-- the identical bootstrap shape) can both pass the "no existing row" check and both attempt the INSERT; the
-- loser hits PK_AccountVault, and because that INSERT sits inside this proc's own BEGIN TRANSACTION, a losing
-- 2627/2601 dooms the WHOLE transaction (XACT_STATE() = -1, verified against Microsoft Learn's
-- TRY...CATCH/XACT_STATE docs) -- a bare TRY/CATCH around just the INSERT is not sufficient. Same
-- ROLLBACK-and-replay idiom already used by game.usp_CharacterLogoutState_Upsert,
-- game.usp_Character_ApplyTribeFourConversion, and game.usp_Cash_Credit_FirstCreditRaceGuard: ROLLBACK
-- TRANSACTION (the only operation a doomed transaction still permits), then a fresh BEGIN TRANSACTION that
-- skips the now-unnecessary AccountVault existence check (the winner's INSERT is guaranteed committed by the
-- time our own INSERT could raise the duplicate-key error) and replays every remaining write this procedure
-- was ever going to make -- the Characters wallet debit/credit, the AccountVault balance credit/debit, and the
-- optional SaveSlotMoney audit row -- inside that same fresh transaction. A THROW 50338/50339 on replay is a
-- legitimate "balance moved in the meantime" outcome, not a bug, matching how
-- usp_Character_ApplyTribeFourConversion's own retry reuses 50355 rather than minting a new number for the
-- same failure kind detected one attempt later.
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
                         @Category = 20, -- EventLogCategory.SaveSlotMoney
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
             @Category = 20, -- EventLogCategory.SaveSlotMoney
             @ActorAccountId = @AccountId,
             @ActorCharacterId = @CharacterId,
             @DeltaMoney = @DeltaCharacterMoney,
             @Quantity = @AuditQuantity,
             @Outcome = 1;

    COMMIT TRANSACTION;
END;
