CREATE PROCEDURE game.usp_Character_SpendBloodCoinAndReplaceContainer @CharacterId INT,
                                                                      @DeltaBloodCoin INT,
                                                                      @Container TINYINT,
                                                                      @TargetSlot TINYINT,
                                                                      @CatalogItemId INT,
                                                                      @CatalogQuantity INT,
                                                                      @Items game.tvp_CharacterItemSlot READONLY
AS
BEGIN
    SET
        NOCOUNT ON;
    SET
        XACT_ABORT ON;

    BEGIN
        TRANSACTION;

    DECLARE @CatalogBundleCost INT,
            @CatalogItemSort TINYINT;

    SELECT TOP (1)
           @CatalogBundleCost = c.Cost,
           @CatalogItemSort = i.Sort
    FROM world.BloodExchangeCatalog AS c WITH (UPDLOCK, HOLDLOCK)
             INNER JOIN world.Items AS i WITH (HOLDLOCK)
                        ON i.ItemId = c.ItemId
    WHERE c.ItemId = @CatalogItemId
      AND c.Cost > 0
      AND CASE WHEN c.Quantity = 0 AND i.Sort = 99 THEN 1 ELSE c.Quantity END = @CatalogQuantity
      AND CONVERT(BIGINT, @DeltaBloodCoin) = -CONVERT(BIGINT, c.Cost)
    ORDER BY c.BloodExchangeSlot;

    IF @CatalogBundleCost IS NULL
        THROW 50271, N'Blood-mark purchase does not match a positive server catalog bundle.', 1;

    IF @Container NOT IN (0, 1) OR @TargetSlot > 63
        THROW 50271, N'Blood-mark purchase has an invalid inventory destination.', 1;

    IF (@CatalogItemSort IN (2, 99) AND @CatalogQuantity NOT BETWEEN 1 AND 999) OR
       (@CatalogItemSort = 22 AND @CatalogQuantity NOT BETWEEN 1 AND 100) OR
       (@CatalogItemSort NOT IN (2, 22, 99) AND @CatalogQuantity NOT IN (0, 1))
        THROW 50271, N'Blood-mark purchase has an invalid catalog bundle quantity.', 1;

    IF EXISTS (SELECT Slot
               FROM @Items
               GROUP BY Slot
               HAVING COUNT(*) > 1)
        THROW 50271, N'Blood-mark purchase contains duplicate inventory slots.', 1;

    DECLARE @Existing TABLE
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

    INSERT INTO @Existing (Slot, ItemId, Quantity, Enchant, Combine, Refine, Socket, SocketGem1, SocketGem2,
                           SocketGem3, ExpireDate, Serial, XPos, YPos)
    SELECT Slot, ItemId, Quantity, Enchant, Combine, Refine, Socket, SocketGem1, SocketGem2, SocketGem3,
           ExpireDate, Serial, XPos, YPos
    FROM game.CharacterItems WITH (UPDLOCK, HOLDLOCK)
    WHERE CharacterId = @CharacterId
      AND Container = @Container;

    DECLARE @ExistingItemId INT,
            @ExistingQuantity INT,
            @ExpectedQuantity INT;

    SELECT @ExistingItemId = ItemId,
           @ExistingQuantity = Quantity
    FROM @Existing
    WHERE Slot = @TargetSlot;

    IF @ExistingItemId IS NULL
        SET @ExpectedQuantity = @CatalogQuantity;
    ELSE IF @CatalogItemSort IN (2, 99) AND @ExistingItemId = @CatalogItemId AND
            @ExistingQuantity BETWEEN 1 AND 999 AND @ExistingQuantity + @CatalogQuantity <= 999
        SET @ExpectedQuantity = @ExistingQuantity + @CatalogQuantity;
    ELSE
        THROW 50271, N'Blood-mark purchase destination no longer permits the catalog bundle.', 1;

    IF NOT EXISTS (SELECT 1
                   FROM @Items
                   WHERE Slot = @TargetSlot
                     AND ItemId = @CatalogItemId
                     AND Quantity = @ExpectedQuantity)
        THROW 50271, N'Blood-mark purchase grant does not match the server catalog bundle.', 1;

    IF EXISTS (SELECT Slot, ItemId, Quantity, Enchant, Combine, Refine, Socket, SocketGem1, SocketGem2,
                      SocketGem3, ExpireDate, Serial, XPos, YPos
               FROM @Existing
               WHERE Slot <> @TargetSlot
               EXCEPT
               SELECT Slot, ItemId, Quantity, Enchant, Combine, Refine, Socket, SocketGem1, SocketGem2,
                      SocketGem3, ExpireDate, Serial, XPos, YPos
               FROM @Items
               WHERE Slot <> @TargetSlot) OR
       EXISTS (SELECT Slot, ItemId, Quantity, Enchant, Combine, Refine, Socket, SocketGem1, SocketGem2,
                      SocketGem3, ExpireDate, Serial, XPos, YPos
               FROM @Items
               WHERE Slot <> @TargetSlot
               EXCEPT
               SELECT Slot, ItemId, Quantity, Enchant, Combine, Refine, Socket, SocketGem1, SocketGem2,
                      SocketGem3, ExpireDate, Serial, XPos, YPos
               FROM @Existing
               WHERE Slot <> @TargetSlot)
        THROW 50271, N'Blood-mark purchase modifies inventory slots outside its destination.', 1;

    DECLARE
        @Debited TABLE
                 (
                     AccountId INT,
                     BloodCoin INT
                 );

    UPDATE game.Characters
    SET BloodCoin    = BloodCoin + @DeltaBloodCoin,
        UpdatedAtUtc = SYSUTCDATETIME()
    OUTPUT INSERTED.AccountId, INSERTED.BloodCoin
        INTO @Debited
    WHERE CharacterId = @CharacterId
      AND BloodCoin + @DeltaBloodCoin >= 0;

    IF
        @@ROWCOUNT = 0
        THROW 50271, N'Unknown character or insufficient BloodCoin balance for this adjustment.', 1;

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

    DECLARE @AccountId INT;
    SELECT @AccountId = AccountId
    FROM @Debited;

    DECLARE @EventPayload NVARCHAR(128) =
        N'Currency=BloodCoin;Delta=' + CONVERT(NVARCHAR(20), @DeltaBloodCoin) +
        N';BundleCost=' + CONVERT(NVARCHAR(20), @CatalogBundleCost);

    EXEC game.usp_EventLog_Insert
         @EventCode = 1,
         @Category = 1,
         @ActorAccountId = @AccountId,
         @ActorCharacterId = @CharacterId,
         @ItemId = @CatalogItemId,
         @Quantity = @CatalogQuantity,
          @Outcome = 1,
          @Payload = @EventPayload;

    SELECT BloodCoin AS NewBloodCoin
    FROM @Debited;

    COMMIT TRANSACTION;
END;
