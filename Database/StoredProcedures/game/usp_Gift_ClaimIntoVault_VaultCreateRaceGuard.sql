CREATE OR ALTER PROCEDURE game.usp_Gift_ClaimIntoVault @GiftId INT,
                                                       @AccountId INT
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    DECLARE
        @Claimed TABLE
                 (
                     ProductId INT NULL,
                     Quantity  INT,
                     Value     INT
                 );

    DECLARE
        @FreeSlot SMALLINT;

    BEGIN TRANSACTION;

    UPDATE game.Gifts
    SET Status = 1
    OUTPUT INSERTED.ProductId,
           INSERTED.Quantity,
           INSERTED.Value
        INTO @Claimed
    WHERE GiftId = @GiftId
      AND AccountId = @AccountId
      AND Status = 0;

    IF
        @@ROWCOUNT = 0
        THROW 50220, N'Gift not found, not owned by this account, or already claimed.', 1;

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

                DELETE FROM @Claimed;

                UPDATE game.Gifts
                SET Status = 1
                OUTPUT INSERTED.ProductId,
                       INSERTED.Quantity,
                       INSERTED.Value
                    INTO @Claimed
                WHERE GiftId = @GiftId
                  AND AccountId = @AccountId
                  AND Status = 0;

                IF @@ROWCOUNT = 0
                    BEGIN
                        ROLLBACK TRANSACTION;
                        THROW 50220, N'Gift not found, not owned by this account, or already claimed (retry).', 1;
                    END;

                SELECT TOP (1) @FreeSlot = Slots.n
                FROM (VALUES (0),
                             (1),
                             (2),
                             (3),
                             (4),
                             (5),
                             (6),
                             (7),
                             (8),
                             (9),
                             (10),
                             (11),
                             (12),
                             (13),
                             (14),
                             (15),
                             (16),
                             (17),
                             (18),
                             (19),
                             (20),
                             (21),
                             (22),
                             (23),
                             (24),
                             (25),
                             (26),
                             (27)) AS Slots(n)
                WHERE NOT EXISTS (SELECT 1
                                  FROM game.AccountVaultItems
                                  WHERE AccountId = @AccountId
                                    AND SlotIndex = Slots.n)
                ORDER BY Slots.n;

                IF @FreeSlot IS NULL
                    BEGIN
                        ROLLBACK TRANSACTION;
                        THROW 50274, N'Account vault is full (28 slots) (retry).', 1;
                    END;

                INSERT INTO game.AccountVaultItems (AccountId, SlotIndex, ItemId, Quantity, Value, SerialNumber,
                                                    SocketData)
                SELECT @AccountId, @FreeSlot, ProductId, Quantity, Value, 0, NULL
                FROM @Claimed;

                INSERT INTO game.GiftLog (AccountId, ProductId, Quantity, Value, Status, CreatedAtUtc)
                SELECT @AccountId, ProductId, Quantity, Value, 1, SYSUTCDATETIME()
                FROM @Claimed;

                SELECT @FreeSlot AS SlotIndex;

                COMMIT TRANSACTION;

                RETURN;
            END CATCH;
        END;

    SELECT TOP (1) @FreeSlot = Slots.n
    FROM (VALUES (0),
                 (1),
                 (2),
                 (3),
                 (4),
                 (5),
                 (6),
                 (7),
                 (8),
                 (9),
                 (10),
                 (11),
                 (12),
                 (13),
                 (14),
                 (15),
                 (16),
                 (17),
                 (18),
                 (19),
                 (20),
                 (21),
                 (22),
                 (23),
                 (24),
                 (25),
                 (26),
                 (27)) AS Slots(n)
    WHERE NOT EXISTS (SELECT 1
                      FROM game.AccountVaultItems
                      WHERE AccountId = @AccountId
                        AND SlotIndex = Slots.n)
    ORDER BY Slots.n;

    IF
        @FreeSlot IS NULL
        THROW 50274, N'Account vault is full (28 slots).', 1;

    INSERT INTO game.AccountVaultItems (AccountId, SlotIndex, ItemId, Quantity, Value, SerialNumber, SocketData)
    SELECT @AccountId, @FreeSlot, ProductId, Quantity, Value, 0, NULL
    FROM @Claimed;

    INSERT INTO game.GiftLog (AccountId, ProductId, Quantity, Value, Status, CreatedAtUtc)
    SELECT @AccountId, ProductId, Quantity, Value, 1, SYSUTCDATETIME()
    FROM @Claimed;

    SELECT @FreeSlot AS SlotIndex;

    COMMIT TRANSACTION;
END;
