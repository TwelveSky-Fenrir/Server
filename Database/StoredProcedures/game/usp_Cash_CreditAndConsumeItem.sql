CREATE PROCEDURE game.usp_Cash_CreditAndConsumeItem @AccountId INT,
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
                                                 ExpireDate, Serial, XPos, YPos)
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
                       Serial,
                       XPos,
                       YPos
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
                                     Refine, Socket, SocketGem1, SocketGem2, SocketGem3, ExpireDate, Serial, XPos, YPos)
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
           Serial,
           XPos,
           YPos
    FROM @Items;

    SELECT BalanceAfter AS NewBalance
    FROM @Credited;

    COMMIT TRANSACTION;
END;
