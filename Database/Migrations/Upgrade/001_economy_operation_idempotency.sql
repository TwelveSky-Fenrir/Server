IF OBJECT_ID(N'game.EconomyOperationEffect', N'U') IS NULL
BEGIN
    CREATE TABLE game.EconomyOperationEffect
    (
        OperationId       UNIQUEIDENTIFIER NOT NULL,
        EffectKind        TINYINT          NOT NULL,
        RequestHash       BINARY(32)       NOT NULL,
        Amount            INT              NOT NULL,
        Reason            TINYINT          NOT NULL,
        ProductId         INT              NOT NULL,
        Container         TINYINT          NOT NULL,
        ItemId            INT              NOT NULL,
        Quantity          INT              NOT NULL,
        Serial            INT              NOT NULL,
        CashBalanceAfter  INT              NOT NULL,
        AppliedAtUtc      DATETIME2(3)     NOT NULL
            CONSTRAINT DF_EconomyOperationEffect_AppliedAtUtc DEFAULT SYSUTCDATETIME(),
        CONSTRAINT PK_EconomyOperationEffect PRIMARY KEY CLUSTERED (OperationId),
        CONSTRAINT FK_EconomyOperationEffect_Operation FOREIGN KEY (OperationId)
            REFERENCES game.EconomyOperationLedger (OperationId),
        CONSTRAINT CK_EconomyOperationEffect_Kind CHECK (EffectKind = 1),
        CONSTRAINT CK_EconomyOperationEffect_Amount CHECK (Amount > 0),
        CONSTRAINT CK_EconomyOperationEffect_Reason CHECK (Reason BETWEEN 1 AND 5),
        CONSTRAINT CK_EconomyOperationEffect_Container CHECK (Container BETWEEN 0 AND 1),
        CONSTRAINT CK_EconomyOperationEffect_Quantity CHECK (Quantity BETWEEN 1 AND 999),
        CONSTRAINT CK_EconomyOperationEffect_CashBalanceAfter CHECK (CashBalanceAfter >= 0)
    );
END;
GO

CREATE OR ALTER PROCEDURE game.usp_EconomyOperation_BeginOrRead @ActorAccountId INT,
                                                                 @ActorCharacterId INT = NULL,
                                                                 @OperationKind TINYINT,
                                                                 @Cause TINYINT,
                                                                 @IdempotencyKeyHash BINARY(32)
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    IF @ActorAccountId <= 0 OR @OperationKind NOT BETWEEN 1 AND 5 OR @Cause NOT BETWEEN 1 AND 5 OR
       @IdempotencyKeyHash IS NULL
        THROW 51300, N'An economy operation requires a valid actor and operation envelope.', 1;

    DECLARE @OperationId UNIQUEIDENTIFIER = NULL;
    DECLARE @CorrelationId UNIQUEIDENTIFIER = NULL;
    DECLARE @Status TINYINT = NULL;
    DECLARE @CreatedAtUtc DATETIME2(3) = NULL;
    DECLARE @CompletedAtUtc DATETIME2(3) = NULL;
    DECLARE @StoredActorCharacterId INT = NULL;
    DECLARE @StoredOperationKind TINYINT = NULL;
    DECLARE @StoredCause TINYINT = NULL;
    DECLARE @Begun BIT = 0;

    BEGIN TRANSACTION;

    IF @ActorCharacterId IS NOT NULL AND NOT EXISTS
    (
        SELECT 1
        FROM game.Characters WITH (UPDLOCK, HOLDLOCK)
        WHERE CharacterId = @ActorCharacterId
          AND AccountId = @ActorAccountId
    )
        THROW 50366, N'The economy operation actor character does not belong to the actor account.', 1;

    SELECT @OperationId = OperationId,
           @CorrelationId = CorrelationId,
           @Status = Status,
           @CreatedAtUtc = CreatedAtUtc,
           @CompletedAtUtc = CompletedAtUtc,
           @StoredActorCharacterId = ActorCharacterId,
           @StoredOperationKind = OperationKind,
           @StoredCause = Cause
    FROM game.EconomyOperationLedger WITH (UPDLOCK, HOLDLOCK)
    WHERE ActorAccountId = @ActorAccountId
      AND IdempotencyKeyHash = @IdempotencyKeyHash;

    IF @OperationId IS NOT NULL
    BEGIN
        IF @StoredActorCharacterId <> @ActorCharacterId OR
           (@StoredActorCharacterId IS NULL AND @ActorCharacterId IS NOT NULL) OR
           (@StoredActorCharacterId IS NOT NULL AND @ActorCharacterId IS NULL) OR
           @StoredOperationKind <> @OperationKind OR @StoredCause <> @Cause
        BEGIN
            ROLLBACK TRANSACTION;
            THROW 51301, N'An economy idempotency key cannot be reused with a different operation envelope.', 1;
        END;

        COMMIT TRANSACTION;

        SELECT @OperationId AS OperationId,
               @CorrelationId AS CorrelationId,
               @Status AS Status,
               @CreatedAtUtc AS CreatedAtUtc,
               @CompletedAtUtc AS CompletedAtUtc,
               CAST(0 AS BIT) AS Begun;
        RETURN;
    END;

    INSERT INTO game.EconomyOperationLedger
        (ActorAccountId, ActorCharacterId, OperationKind, Cause, IdempotencyKeyHash)
    VALUES
        (@ActorAccountId, @ActorCharacterId, @OperationKind, @Cause, @IdempotencyKeyHash);

    SELECT @OperationId = OperationId,
           @CorrelationId = CorrelationId,
           @Status = Status,
           @CreatedAtUtc = CreatedAtUtc,
           @CompletedAtUtc = CompletedAtUtc
    FROM game.EconomyOperationLedger
    WHERE ActorAccountId = @ActorAccountId
      AND IdempotencyKeyHash = @IdempotencyKeyHash;

    SET @Begun = 1;

    COMMIT TRANSACTION;

    SELECT @OperationId AS OperationId,
           @CorrelationId AS CorrelationId,
           @Status AS Status,
           @CreatedAtUtc AS CreatedAtUtc,
           @CompletedAtUtc AS CompletedAtUtc,
           @Begun AS Begun;
END;
GO

CREATE OR ALTER PROCEDURE game.usp_EconomyOperation_Complete @OperationId UNIQUEIDENTIFIER,
                                                              @ActorAccountId INT,
                                                              @FinalStatus TINYINT
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    IF @OperationId = '00000000-0000-0000-0000-000000000000' OR @ActorAccountId <= 0 OR
       @FinalStatus NOT BETWEEN 1 AND 3
        THROW 51302, N'An economy operation completion requires a valid terminal envelope.', 1;

    DECLARE @CorrelationId UNIQUEIDENTIFIER = NULL;
    DECLARE @Status TINYINT = NULL;
    DECLARE @CreatedAtUtc DATETIME2(3) = NULL;
    DECLARE @CompletedAtUtc DATETIME2(3) = NULL;
    DECLARE @CompletedNow BIT = 0;

    BEGIN TRANSACTION;

    SELECT @CorrelationId = CorrelationId,
           @Status = Status,
           @CreatedAtUtc = CreatedAtUtc,
           @CompletedAtUtc = CompletedAtUtc
    FROM game.EconomyOperationLedger WITH (UPDLOCK, HOLDLOCK)
    WHERE OperationId = @OperationId
      AND ActorAccountId = @ActorAccountId;

    IF @CorrelationId IS NULL
        THROW 50368, N'The economy operation does not exist for the specified actor account.', 1;

    IF @Status = 0
    BEGIN
        UPDATE game.EconomyOperationLedger
        SET Status = @FinalStatus,
            CompletedAtUtc = SYSUTCDATETIME()
        WHERE OperationId = @OperationId
          AND ActorAccountId = @ActorAccountId
          AND Status = 0;

        SET @CompletedNow = 1;

        SELECT @Status = Status,
               @CompletedAtUtc = CompletedAtUtc
        FROM game.EconomyOperationLedger
        WHERE OperationId = @OperationId
          AND ActorAccountId = @ActorAccountId;
    END
    ELSE IF @Status <> @FinalStatus
    BEGIN
        ROLLBACK TRANSACTION;
        THROW 51303, N'An economy operation cannot be completed with a different terminal status.', 1;
    END;

    COMMIT TRANSACTION;

    SELECT @OperationId AS OperationId,
           @CorrelationId AS CorrelationId,
           @Status AS Status,
           @CreatedAtUtc AS CreatedAtUtc,
           @CompletedAtUtc AS CompletedAtUtc,
           @CompletedNow AS CompletedNow;
END;
GO

CREATE OR ALTER PROCEDURE game.usp_Cash_DebitAndGrantItem_Idempotent @OperationId UNIQUEIDENTIFIER,
                                                                      @IdempotencyKeyHash BINARY(32),
                                                                      @RequestHash BINARY(32),
                                                                      @AccountId INT,
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
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    IF @OperationId = '00000000-0000-0000-0000-000000000000' OR @IdempotencyKeyHash IS NULL OR
       @RequestHash IS NULL OR @AccountId <= 0 OR @Reason NOT BETWEEN 1 AND 5
        THROW 51304, N'An idempotent cash purchase requires a valid operation envelope.', 1;

    IF @Amount < 1 OR @AuditItemId IS NULL OR @AuditQuantity NOT BETWEEN 1 AND 999 OR
       @Container NOT BETWEEN 0 AND 1
        THROW 50241, N'Cash item grant parameters are invalid.', 1;

    DECLARE @ExistingOperationId UNIQUEIDENTIFIER = NULL;
    DECLARE @ExistingCharacterId INT = NULL;
    DECLARE @ExistingStatus TINYINT = NULL;
    DECLARE @ExistingEffectKind TINYINT = NULL;
    DECLARE @ExistingRequestHash BINARY(32) = NULL;
    DECLARE @ExistingAmount INT = NULL;
    DECLARE @ExistingReason TINYINT = NULL;
    DECLARE @ExistingProductId INT = NULL;
    DECLARE @ExistingContainer TINYINT = NULL;
    DECLARE @ExistingItemId INT = NULL;
    DECLARE @ExistingQuantity INT = NULL;
    DECLARE @ExistingSerial INT = NULL;
    DECLARE @ExistingBalanceAfter INT = NULL;

    BEGIN TRANSACTION;

    SELECT @ExistingOperationId = OperationId,
           @ExistingCharacterId = ActorCharacterId,
           @ExistingStatus = Status
    FROM game.EconomyOperationLedger WITH (UPDLOCK, HOLDLOCK)
    WHERE ActorAccountId = @AccountId
      AND IdempotencyKeyHash = @IdempotencyKeyHash;

    IF @ExistingOperationId IS NOT NULL
    BEGIN
        SELECT @ExistingEffectKind = EffectKind,
               @ExistingRequestHash = RequestHash,
               @ExistingAmount = Amount,
               @ExistingReason = Reason,
               @ExistingProductId = ProductId,
               @ExistingContainer = Container,
               @ExistingItemId = ItemId,
               @ExistingQuantity = Quantity,
               @ExistingSerial = Serial,
               @ExistingBalanceAfter = CashBalanceAfter
        FROM game.EconomyOperationEffect WITH (UPDLOCK, HOLDLOCK)
        WHERE OperationId = @ExistingOperationId;

        IF @ExistingOperationId <> @OperationId OR @ExistingCharacterId IS NULL OR
           @ExistingCharacterId <> @CharacterId OR @ExistingStatus <> 1 OR @ExistingEffectKind IS NULL OR
           @ExistingEffectKind <> 1 OR @ExistingRequestHash IS NULL OR @ExistingRequestHash <> @RequestHash OR
           @ExistingAmount <> @Amount OR @ExistingReason <> @Reason OR @ExistingProductId <> @ProductId OR
           @ExistingContainer <> @Container OR @ExistingItemId <> @AuditItemId OR
           @ExistingQuantity <> @AuditQuantity OR @ExistingSerial <> @AuditSerial OR @ExistingBalanceAfter IS NULL
        BEGIN
            ROLLBACK TRANSACTION;
            THROW 51305, N'An idempotent cash purchase was replayed with a different or incomplete operation envelope.', 1;
        END;

        COMMIT TRANSACTION;

        SELECT @ExistingBalanceAfter AS NewBalance,
               CAST(1 AS BIT) AS WasAlreadyApplied;
        RETURN;
    END;

    IF EXISTS
    (
        SELECT 1
        FROM game.EconomyOperationLedger WITH (UPDLOCK, HOLDLOCK)
        WHERE OperationId = @OperationId
    )
        THROW 51305, N'An idempotent cash purchase operation identifier is already bound to another operation.', 1;

    IF NOT EXISTS
    (
        SELECT 1
        FROM game.Characters WITH (UPDLOCK, HOLDLOCK)
        WHERE CharacterId = @CharacterId
          AND AccountId = @AccountId
    )
        THROW 50241, N'Cash item grant character must belong to the debited account.', 1;

    DECLARE @CatalogItemId INT = NULL;
    DECLARE @CatalogQuantity INT = NULL;
    DECLARE @CatalogCost INT = NULL;
    DECLARE @ItemSort TINYINT = NULL;

    SELECT @CatalogItemId = p.ItemId,
           @CatalogQuantity = p.Quantity,
           @CatalogCost = p.Cost,
           @ItemSort = i.Sort
    FROM world.ItemMallProducts AS p WITH (UPDLOCK, HOLDLOCK)
    INNER JOIN world.Items AS i ON i.ItemId = p.ItemId
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
       @UnitsPerPurchase > 999 OR @AuditQuantity % @UnitsPerPurchase <> 0 OR
       @AuditQuantity / @UnitsPerPurchase NOT BETWEEN 1 AND 99
        THROW 50241, N'Cash item grant quantity does not match the bounded catalog purchase quantity.', 1;

    DECLARE @ExpectedAmount BIGINT = CAST(@CatalogCost AS BIGINT) * @AuditQuantity;

    IF @CatalogCost < 1 OR @ExpectedAmount > 2147483647 OR @Amount <> @ExpectedAmount
        THROW 50241, N'Cash debit does not match the authoritative catalog price and granted quantity.', 1;

    DECLARE @Debited TABLE (BalanceAfter INT NOT NULL);

    UPDATE game.AccountCash
    SET Balance = Balance - @Amount,
        UpdatedAtUtc = SYSUTCDATETIME()
    OUTPUT INSERTED.Balance INTO @Debited (BalanceAfter)
    WHERE AccountId = @AccountId
      AND Balance >= @Amount;

    IF @@ROWCOUNT = 0
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

    INSERT INTO @ExistingItems
        (Slot, ItemId, Quantity, Enchant, Combine, Refine, Socket, SocketGem1, SocketGem2, SocketGem3,
         ExpireDate, Serial, XPos, YPos)
    SELECT Slot, ItemId, Quantity, Enchant, Combine, Refine, Socket, SocketGem1, SocketGem2, SocketGem3,
           ExpireDate, Serial, XPos, YPos
    FROM game.CharacterItems WITH (UPDLOCK, HOLDLOCK)
    WHERE CharacterId = @CharacterId
      AND Container = @Container;

    IF EXISTS (SELECT 1 FROM @Items GROUP BY Slot HAVING COUNT(*) > 1) OR
       EXISTS
       (
           SELECT 1
           FROM @Items
           WHERE Slot > 63 OR Quantity NOT BETWEEN 0 AND 999 OR XPos NOT BETWEEN 0 AND 7 OR
                 YPos NOT BETWEEN 0 AND 7
       )
        THROW 50241, N'Cash item grant supplied an invalid inventory snapshot.', 1;

    DECLARE @GrantSlots TABLE (Slot TINYINT NOT NULL PRIMARY KEY);

    INSERT INTO @GrantSlots (Slot)
    SELECT incoming.Slot
    FROM @Items AS incoming
    LEFT JOIN @ExistingItems AS existing ON existing.Slot = incoming.Slot
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

    IF EXISTS
    (
        SELECT 1
        FROM @ExistingItems AS existing
        FULL OUTER JOIN @Items AS incoming ON incoming.Slot = existing.Slot
        WHERE COALESCE(existing.Slot, incoming.Slot) <> @GrantSlot
          AND (existing.Slot IS NULL OR incoming.Slot IS NULL OR existing.ItemId <> incoming.ItemId OR
               existing.Quantity <> incoming.Quantity OR existing.Enchant <> incoming.Enchant OR
               existing.Combine <> incoming.Combine OR existing.Refine <> incoming.Refine OR
               existing.Socket <> incoming.Socket OR existing.SocketGem1 <> incoming.SocketGem1 OR
               existing.SocketGem2 <> incoming.SocketGem2 OR existing.SocketGem3 <> incoming.SocketGem3 OR
               existing.ExpireDate <> incoming.ExpireDate OR existing.Serial <> incoming.Serial OR
               existing.XPos <> incoming.XPos OR existing.YPos <> incoming.YPos)
    )
        THROW 50241, N'Cash item grant may not replace or alter unrelated inventory slots.', 1;

    INSERT INTO game.CashLog (AccountId, Delta, BalanceAfter, Reason, ProductId)
    SELECT @AccountId, -@Amount, BalanceAfter, @Reason, @ProductId
    FROM @Debited;

    DELETE FROM game.CharacterItems
    WHERE CharacterId = @CharacterId
      AND Container = @Container
      AND Slot = @GrantSlot;

    INSERT INTO game.CharacterItems
        (CharacterId, Container, Slot, ItemId, Quantity, Enchant, Combine, Refine, Socket, SocketGem1,
         SocketGem2, SocketGem3, ExpireDate, Serial, XPos, YPos)
    SELECT @CharacterId, @Container, Slot, ItemId, Quantity, Enchant, Combine, Refine, Socket, SocketGem1,
           SocketGem2, SocketGem3, ExpireDate, Serial, XPos, YPos
    FROM @Items
    WHERE Slot = @GrantSlot;

    DECLARE @AuditPayload NVARCHAR(MAX) = CASE
                                               WHEN @AuditSerial = 0 THEN NULL
                                               ELSE N'Serial=' + CONVERT(NVARCHAR(20), @AuditSerial)
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

    INSERT INTO game.EconomyOperationLedger
        (OperationId, ActorAccountId, ActorCharacterId, OperationKind, Cause, IdempotencyKeyHash, Status,
         CompletedAtUtc)
    VALUES
        (@OperationId, @AccountId, @CharacterId, 4, @Reason, @IdempotencyKeyHash, 1, SYSUTCDATETIME());

    INSERT INTO game.EconomyOperationEffect
        (OperationId, EffectKind, RequestHash, Amount, Reason, ProductId, Container, ItemId, Quantity, Serial,
         CashBalanceAfter)
    SELECT @OperationId, 1, @RequestHash, @Amount, @Reason, @ProductId, @Container, @AuditItemId,
           @AuditQuantity, @AuditSerial, BalanceAfter
    FROM @Debited;

    COMMIT TRANSACTION;

    SELECT BalanceAfter AS NewBalance,
           CAST(0 AS BIT) AS WasAlreadyApplied
    FROM @Debited;
END;
