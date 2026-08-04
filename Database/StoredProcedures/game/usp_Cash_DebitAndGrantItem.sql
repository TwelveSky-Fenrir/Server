CREATE PROCEDURE game.usp_Cash_DebitAndGrantItem @AccountId INT,
                                                 @Amount INT,
                                                 @Reason TINYINT,
                                                 @ProductId INT,
                                                 @CharacterId INT,
                                                 @Container TINYINT,
                                                 @Items game.tvp_CharacterItemSlot READONLY,
                                                 @AuditItemId INT,
                                                 @AuditQuantity INT,
                                                 @AuditSerial INT
AS
BEGIN
    SET
        NOCOUNT ON;
    SET
        XACT_ABORT ON;

    IF
        @Amount < 1
        THROW 50241, N'Cash amount must be positive.', 1;

    IF @AuditItemId IS NULL OR @AuditQuantity NOT BETWEEN 1 AND 999
        THROW 50241, N'Cash item grant must have a positive, bounded audit quantity.', 1;

    IF @Container NOT BETWEEN 0 AND 1
        THROW 50241, N'Cash item grant must target an inventory container.', 1;

    IF NOT EXISTS (SELECT 1
                   FROM game.Characters
                   WHERE CharacterId = @CharacterId
                     AND AccountId = @AccountId)
        THROW 50241, N'Cash item grant character must belong to the debited account.', 1;

    BEGIN
        TRANSACTION;

    DECLARE @CatalogItemId INT,
        @CatalogQuantity INT,
        @CatalogCost INT,
        @ItemSort TINYINT;

    SELECT @CatalogItemId = p.ItemId,
           @CatalogQuantity = p.Quantity,
           @CatalogCost = p.Cost,
           @ItemSort = i.Sort
    FROM world.ItemMallProducts p WITH (UPDLOCK, HOLDLOCK)
             INNER JOIN world.Items i ON i.ItemId = p.ItemId
    WHERE p.ItemMallProductId = @ProductId
      AND p.ProductType BETWEEN 1 AND 4
      AND p.IsActive = 1;

    IF @CatalogItemId IS NULL OR @CatalogItemId <> @AuditItemId
        THROW 50241, N'Cash product is not an active catalog entry for the granted item.', 1;

    DECLARE @UnitsPerPurchase INT = CASE
                                        WHEN @ItemSort IN (2, 99) AND @CatalogQuantity > 0 THEN @CatalogQuantity
                                        ELSE 1
        END;

    IF (@ItemSort IN (2, 99) AND @CatalogQuantity NOT BETWEEN 0 AND 999) OR
       @UnitsPerPurchase > 999 OR
       @AuditQuantity % @UnitsPerPurchase <> 0 OR
       @AuditQuantity / @UnitsPerPurchase NOT BETWEEN 1 AND 99
        THROW 50241, N'Cash item grant quantity does not match the bounded catalog purchase quantity.', 1;

    DECLARE @ExpectedAmount BIGINT = CAST(@CatalogCost AS BIGINT) * @AuditQuantity;

    IF @CatalogCost < 1 OR @ExpectedAmount > 2147483647 OR @Amount <> @ExpectedAmount
        THROW 50241, N'Cash debit does not match the authoritative catalog price and granted quantity.', 1;

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

    DECLARE @ExistingItems TABLE
                           (
                               Slot       TINYINT NOT NULL PRIMARY KEY,
                               ItemId     INT     NOT NULL,
                               Quantity   INT     NOT NULL,
                               Enchant    TINYINT NOT NULL,
                               Combine    TINYINT NOT NULL,
                               Refine     TINYINT NOT NULL,
                               Socket     TINYINT NOT NULL,
                               SocketGem1 INT     NOT NULL,
                               SocketGem2 INT     NOT NULL,
                               SocketGem3 INT     NOT NULL,
                               ExpireDate INT     NOT NULL,
                               Serial     INT     NOT NULL,
                               XPos       TINYINT NOT NULL,
                               YPos       TINYINT NOT NULL
                           );

    INSERT INTO @ExistingItems (Slot, ItemId, Quantity, Enchant, Combine, Refine, Socket, SocketGem1, SocketGem2,
                                SocketGem3, ExpireDate, Serial, XPos, YPos)
    SELECT Slot,
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
    FROM game.CharacterItems
    WITH (UPDLOCK, HOLDLOCK)
    WHERE CharacterId = @CharacterId
      AND Container = @Container;

    IF EXISTS (SELECT 1
               FROM @Items
               GROUP BY Slot
               HAVING COUNT(*) > 1) OR
       EXISTS (SELECT 1
               FROM @Items
               WHERE Slot > 63
                  OR Quantity NOT BETWEEN 0 AND 999
                  OR XPos NOT BETWEEN 0 AND 7
                  OR YPos NOT BETWEEN 0 AND 7)
        THROW 50241, N'Cash item grant supplied an invalid inventory snapshot.', 1;

    DECLARE @GrantSlots TABLE
                        (
                            Slot TINYINT NOT NULL PRIMARY KEY
                        );

    INSERT INTO @GrantSlots (Slot)
    SELECT incoming.Slot
    FROM @Items incoming
             LEFT JOIN @ExistingItems existing ON existing.Slot = incoming.Slot
    WHERE incoming.ItemId = @AuditItemId
      AND incoming.Enchant = 0
      AND incoming.Combine = 0
      AND incoming.Refine = 0
      AND incoming.Socket = 0
      AND incoming.SocketGem1 = 0
      AND incoming.SocketGem2 = 0
      AND incoming.SocketGem3 = 0
      AND incoming.ExpireDate = 0
      AND ((existing.Slot IS NULL AND incoming.Quantity = @AuditQuantity AND incoming.Serial = @AuditSerial) OR
           (existing.Slot IS NOT NULL AND existing.ItemId = @AuditItemId AND
            existing.Quantity BETWEEN 1 AND 999 AND incoming.Quantity = existing.Quantity + @AuditQuantity AND
            incoming.XPos = existing.XPos AND incoming.YPos = existing.YPos AND incoming.Serial = 0 AND
            @AuditSerial = 0));

    IF (SELECT COUNT(*) FROM @GrantSlots) <> 1
        THROW 50241, N'Cash item grant must change exactly one compatible inventory slot.', 1;

    DECLARE @GrantSlot TINYINT = (SELECT Slot FROM @GrantSlots);

    IF EXISTS (SELECT 1
               FROM @ExistingItems existing
                        FULL OUTER JOIN @Items incoming ON incoming.Slot = existing.Slot
               WHERE COALESCE(existing.Slot, incoming.Slot) <> @GrantSlot
                 AND (existing.Slot IS NULL OR incoming.Slot IS NULL OR existing.ItemId <> incoming.ItemId OR
                      existing.Quantity <> incoming.Quantity OR existing.Enchant <> incoming.Enchant OR
                      existing.Combine <> incoming.Combine OR existing.Refine <> incoming.Refine OR
                      existing.Socket <> incoming.Socket OR existing.SocketGem1 <> incoming.SocketGem1 OR
                      existing.SocketGem2 <> incoming.SocketGem2 OR existing.SocketGem3 <> incoming.SocketGem3 OR
                      existing.ExpireDate <> incoming.ExpireDate OR existing.Serial <> incoming.Serial OR
                      existing.XPos <> incoming.XPos OR existing.YPos <> incoming.YPos))
        THROW 50241, N'Cash item grant may not replace or alter unrelated inventory slots.', 1;

    INSERT INTO game.CashLog (AccountId, Delta, BalanceAfter, Reason, ProductId)
    SELECT @AccountId, -@Amount, BalanceAfter, @Reason, @ProductId
    FROM @Debited;

    DELETE
    FROM game.CharacterItems
    WHERE CharacterId = @CharacterId
      AND Container = @Container
      AND Slot = @GrantSlot;

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
    FROM @Items
    WHERE Slot = @GrantSlot;

    DECLARE @AuditPayload NVARCHAR(MAX) = CASE
                                              WHEN @AuditSerial = 0 THEN NULL
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

    SELECT BalanceAfter AS NewBalance
    FROM @Debited;

    COMMIT TRANSACTION;
END;
