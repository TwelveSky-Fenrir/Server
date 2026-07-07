-- Same debit contract as usp_Cash_Debit, plus a whole-container item grant in the same transaction, so
-- a fault between debit and grant can never take payment without delivering the item.
CREATE PROCEDURE game.usp_Cash_DebitAndGrantItem @AccountId INT,
                                                 @Amount INT,
                                                 @Reason TINYINT,
                                                 @ProductId INT,
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
        @Debited TABLE
                 (
                     BalanceAfter INT
                 );

    UPDATE game.AccountCash
    SET Balance      = Balance - @Amount,
        UpdatedAtUtc = SYSUTCDATETIME()
    OUTPUT INSERTED.Balance
        INTO @Debited
    WHERE AccountId = @AccountId
      AND Balance >= @Amount;

    IF
        @@ROWCOUNT = 0
        THROW 50240, N'Insufficient cash balance for this debit.', 1;

    INSERT INTO game.CashLog (AccountId, Delta, BalanceAfter, Reason, ProductId)
    SELECT @AccountId, -@Amount, BalanceAfter, @Reason, @ProductId
    FROM @Debited;

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
    FROM @Debited;

    COMMIT TRANSACTION;
END;
