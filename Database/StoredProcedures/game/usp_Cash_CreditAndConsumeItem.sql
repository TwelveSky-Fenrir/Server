-- Credits game.AccountCash, plus a whole-container item-consumption in the same transaction, so a fault
-- between the credit and the consumption can never dupe cash while leaving the consumed item still
-- physically present. Mirrors usp_Cash_DebitAndGrantItem's existing shape in the opposite direction (that
-- procedure's own header comment explains why a bare single-purpose debit/credit procedure isn't enough
-- once a container mutation must be atomic with it). Closes a transaction-composition-audit finding:
-- GP-ticket redemption previously called usp_Cash_Credit and a separate, unguarded
-- usp_CharacterItems_ReplaceContainer round trip from C#; a transient failure on the second call let cash
-- durably commit while the ticket item never left the character's inventory -- a repeatable premium-currency
-- duplication vector on client retry (a client resend of the identical "use item" request re-credited cash
-- again for the same physical item). Reuses error 50241 ("cash amount must be positive") rather than
-- registering a new admin.ErrorCatalog row, since it is the identical precondition already thrown by both
-- usp_Cash_Debit and usp_Cash_Credit for the same failure kind.
CREATE PROCEDURE game.usp_Cash_CreditAndConsumeItem @AccountId INT,
                                                     @Amount INT,
                                                     @Reason TINYINT,
                                                     @ProductId INT = NULL,
                                                     @CharacterId INT,
                                                     @Container TINYINT,
                                                     @Items game.tvp_CharacterItemSlot READONLY
AS
BEGIN
    SET
        NOCOUNT ON;
    SET
        XACT_ABORT ON;

    IF
        @Amount < 1
        THROW 50241, N'Cash amount must be positive.', 1;

    BEGIN
        TRANSACTION;

    DECLARE
        @Credited TABLE
                  (
                      BalanceAfter INT
                  );

    UPDATE game.AccountCash
    SET Balance      = Balance + @Amount,
        UpdatedAtUtc = SYSUTCDATETIME()
    OUTPUT INSERTED.Balance
        INTO @Credited
    WHERE AccountId = @AccountId;

    IF
        @@ROWCOUNT = 0
        BEGIN
            INSERT INTO game.AccountCash (AccountId, Balance)
            VALUES (@AccountId, @Amount);

            INSERT INTO @Credited (BalanceAfter)
            VALUES (@Amount);
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
