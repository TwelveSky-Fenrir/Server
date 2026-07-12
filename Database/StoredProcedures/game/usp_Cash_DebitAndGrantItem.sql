-- Debits game.AccountCash, plus a whole-container item grant in the same transaction, so a fault between
-- debit and grant can never take payment without delivering the item. A bare single-purpose debit procedure
-- (usp_Cash_Debit) used to exist but had zero callers -- every item-mall/cash-shop debit path also grants
-- an item, so the bare version was never needed; removed as dead code in the 2026-07-12 Database/ cleanup
-- pass.
--
-- @Audit* params optionally nest the CashShopPurchase EventLog row (EventLogCategory.CashShopPurchase = 22)
-- into this same transaction, following usp_Character_AdjustMoneyAndReplaceContainer's own precedent
-- (transaction-composition-audit finding) for why the audit write and the money movement it documents must
-- commit or roll back together instead of a second, unshared round trip from the caller. Before this, that
-- round trip (BuyCashItemService -> IEventLogRepository.LogCashShopPurchaseAsync) ran AFTER this procedure
-- already committed, so a crash between the two calls silently dropped the general audit-feed row while
-- game.CashLog's own row (the domain-authoritative record of the balance movement) still landed. Caller
-- omits @AuditItemId (stays NULL) to skip the nested write entirely -- no other caller of this procedure
-- exists today, but any future one inherits the same opt-out. @DeltaMoney is deliberately NOT passed to the
-- nested EXEC (left NULL, matching the original C# emitter's own behavior): game.CashLog is the sole
-- authoritative ledger for the amount charged, EventLog's CashShopPurchase row only ever tracked which item
-- was granted, not the price.
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

    IF @AuditItemId IS NOT NULL
    BEGIN
        DECLARE @AuditPayload NVARCHAR(MAX) = CASE
                                                   WHEN @AuditSerial IS NULL OR @AuditSerial = 0 THEN NULL
                                                   ELSE N'Serial=' + CAST(@AuditSerial AS NVARCHAR(20))
            END;

        EXEC game.usp_EventLog_Insert
             @EventCode = 1, -- EventLogEmitters.CashShopPurchaseEventCode
             @Category = 22, -- EventLogCategory.CashShopPurchase
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
