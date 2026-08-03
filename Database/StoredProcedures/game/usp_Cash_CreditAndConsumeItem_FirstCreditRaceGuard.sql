-- Additive script: usp_Cash_CreditAndConsumeItem_UpperBoundGuard.sql stays unchanged (DbMigrator journals
-- it by SHA-256 and would refuse to reapply it if edited). CREATE OR ALTER on the same procedure name, same
-- pattern as usp_Cash_Credit_FirstCreditRaceGuard.sql (that file's own header carries the full race
-- explanation this one shares).
--
-- Same first-credit-ever race as usp_Cash_Credit_FirstCreditRaceGuard.sql, on the identical
-- game.AccountCash bootstrap INSERT this procedure shares with usp_Cash_Credit: two concurrent first-ever
-- credits for the SAME @AccountId can both pass the "no existing row" check and both attempt the INSERT;
-- the loser hits PK_AccountCash, and because that INSERT sits inside this proc's own explicit
-- BEGIN TRANSACTION under XACT_ABORT ON, a losing 2627/2601 dooms the whole transaction (XACT_STATE() = -1)
-- -- including the CharacterItems container replace and the CashLog row that would otherwise follow. Same
-- ROLLBACK-and-replay idiom as game.usp_CharacterLogoutState_Upsert / game.usp_Character_ApplyTribeFourConversion
-- and the sibling fix: ROLLBACK TRANSACTION, then a fresh BEGIN TRANSACTION that re-runs the guarded UPDATE
-- (now against the winner's committed row), then replays every write this procedure was ever going to make
-- -- CashLog insert, container DELETE+INSERT from @Items, and the NewBalance SELECT -- inside that same
-- fresh transaction, so the GP-ticket-redemption call site still gets its item granted and its balance read
-- back exactly once, never a partial commit of one half without the other.
CREATE OR ALTER PROCEDURE game.usp_Cash_CreditAndConsumeItem @AccountId INT,
                                                             @Amount INT,
                                                             @Reason TINYINT,
                                                             @ProductId INT = NULL,
                                                             @CharacterId INT,
                                                             @Container TINYINT,
                                                             @Items game.tvp_CharacterItemSlot READONLY
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    IF @Amount < 1
        THROW 50241, N'Cash amount must be positive.', 1;

    BEGIN TRANSACTION;

    DECLARE @Credited TABLE
                      (
                          BalanceAfter INT
                      );

    UPDATE game.AccountCash
    SET Balance      = Balance + @Amount,
        UpdatedAtUtc = SYSUTCDATETIME()
    OUTPUT INSERTED.Balance
        INTO @Credited
    WHERE AccountId = @AccountId
      AND Balance + @Amount <= 2000000000;

    IF @@ROWCOUNT = 0
        BEGIN
            IF EXISTS (SELECT 1 FROM game.AccountCash WHERE AccountId = @AccountId)
                THROW 50360, N'Crediting this account''s cash balance would exceed the legacy cash cap (2,000,000,000).', 1;

            IF @Amount > 2000000000
                THROW 50360, N'Crediting this account''s cash balance would exceed the legacy cash cap (2,000,000,000).', 1;

            BEGIN TRY
                INSERT INTO game.AccountCash (AccountId, Balance)
                VALUES (@AccountId, @Amount);

                INSERT INTO @Credited (BalanceAfter)
                VALUES (@Amount);
            END TRY
            BEGIN CATCH
                IF ERROR_NUMBER() NOT IN (2627, 2601)
                    THROW;

                ROLLBACK TRANSACTION;

                BEGIN TRANSACTION;

                UPDATE game.AccountCash
                SET Balance      = Balance + @Amount,
                    UpdatedAtUtc = SYSUTCDATETIME()
                OUTPUT INSERTED.Balance
                    INTO @Credited
                WHERE AccountId = @AccountId
                  AND Balance + @Amount <= 2000000000;

                IF @@ROWCOUNT = 0
                    BEGIN
                        ROLLBACK TRANSACTION;
                        THROW 50360,
                            N'Crediting this account''s cash balance would exceed the legacy cash cap (2,000,000,000) (retry).', 1;
                    END;

                INSERT INTO game.CashLog (AccountId, Delta, BalanceAfter, Reason, ProductId)
                SELECT @AccountId, @Amount, BalanceAfter, @Reason, @ProductId
                FROM @Credited;

                DELETE
                FROM game.CharacterItems
                WHERE CharacterId = @CharacterId
                  AND Container = @Container;

                INSERT INTO game.CharacterItems (CharacterId, Container, Slot, ItemId, Quantity, Enchant,
                                                 Combine, Refine, Socket, SocketGem1, SocketGem2, SocketGem3,
                                                 ExpireDate, Serial)
                SELECT @CharacterId,
                       @Container,
                       Slot,
                       ItemId,
                       Quantity,
                       Enchant,
                       Combine,
                       Refine,
                       Socket,
                       SocketGem1,
                       SocketGem2,
                       SocketGem3,
                       ExpireDate,
                       Serial
                FROM @Items;

                SELECT BalanceAfter AS NewBalance
                FROM @Credited;

                COMMIT TRANSACTION;

                RETURN;
            END CATCH;
        END;

    INSERT INTO game.CashLog (AccountId, Delta, BalanceAfter, Reason, ProductId)
    SELECT @AccountId, @Amount, BalanceAfter, @Reason, @ProductId
    FROM @Credited;

    DELETE
    FROM game.CharacterItems
    WHERE CharacterId = @CharacterId
      AND Container = @Container;

    INSERT INTO game.CharacterItems (CharacterId, Container, Slot, ItemId, Quantity, Enchant, Combine,
                                     Refine, Socket, SocketGem1, SocketGem2, SocketGem3, ExpireDate, Serial)
    SELECT @CharacterId,
           @Container,
           Slot,
           ItemId,
           Quantity,
           Enchant,
           Combine,
           Refine,
           Socket,
           SocketGem1,
           SocketGem2,
           SocketGem3,
           ExpireDate,
           Serial
    FROM @Items;

    SELECT BalanceAfter AS NewBalance
    FROM @Credited;

    COMMIT TRANSACTION;
END;
