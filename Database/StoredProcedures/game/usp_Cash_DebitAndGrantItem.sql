CREATE PROCEDURE game.usp_Cash_DebitAndGrantItem @AccountId INT,
                                                 @Amount INT,
                                                 @Reason TINYINT,
                                                 @ProductId INT,
                                                 @CharacterId INT,
                                                 @Container TINYINT,
                                                 @Items game.tvp_CharacterItemSlot READONLY,
                                                 @AuditItemId INT = NULL,
                                                 @AuditQuantity INT = NULL,
                                                 @AuditSerial INT = NULL
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

    IF @AuditItemId IS NOT NULL
        BEGIN
            DECLARE @AuditPayload NVARCHAR(MAX) = CASE
                                                      WHEN @AuditSerial IS NULL OR @AuditSerial = 0 THEN NULL
                                                      ELSE N'Serial=' + CAST(@AuditSerial AS NVARCHAR(20))
                END;

            EXEC game.usp_EventLog_Insert
                 @EventCode = 1,
                 @Category = 22,
                 @ActorAccountId = @AccountId,
                 @ActorCharacterId = @CharacterId,
                 @ItemId = @AuditItemId,
                 @Quantity = @AuditQuantity,
                 @Outcome = 1,
                 @Payload = @AuditPayload;
        END;

    SELECT BalanceAfter AS NewBalance
    FROM @Debited;

    COMMIT TRANSACTION;
END;
