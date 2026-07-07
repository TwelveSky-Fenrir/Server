-- Creates the account's AccountVault row if missing (a gift claim must not require the vault panel to
-- have been opened first). On a full vault (50274), the transaction rolls back and the gift stays Pending.
CREATE PROCEDURE game.usp_Gift_ClaimIntoVault @GiftId INT,
                                              @AccountId INT
AS
BEGIN
    SET
        NOCOUNT ON;
    SET
        XACT_ABORT ON;

    BEGIN
        TRANSACTION;

    DECLARE
        @Claimed TABLE
                 (
                     ProductId INT NULL,
                     Quantity  INT,
                     Value     INT
                 );

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

    IF
        NOT EXISTS (SELECT 1 FROM game.AccountVault WHERE AccountId = @AccountId)
        INSERT INTO game.AccountVault (AccountId) VALUES (@AccountId);

    DECLARE
        @FreeSlot SMALLINT;

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
