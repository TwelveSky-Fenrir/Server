CREATE PROCEDURE game.usp_Gift_Enqueue @AccountId INT,
                                       @ProductId INT = NULL,
                                       @Quantity INT,
                                       @Value INT
AS
BEGIN
    SET
        NOCOUNT ON;
    SET
        XACT_ABORT ON;

    DECLARE
        @Now DATETIME2(3) = SYSUTCDATETIME();
    DECLARE
        @Inserted TABLE
                  (
                      GiftId INT
                  );

    BEGIN
        TRANSACTION;

    INSERT INTO game.Gifts (AccountId, ProductId, Quantity, Value, Status, CreatedAtUtc)
    OUTPUT INSERTED.GiftId INTO @Inserted (GiftId)
    VALUES (@AccountId, @ProductId, @Quantity, @Value, 0, @Now);

    INSERT INTO game.GiftLog (AccountId, ProductId, Quantity, Value, Status, CreatedAtUtc)
    VALUES (@AccountId, @ProductId, @Quantity, @Value, 0, @Now);

    SELECT GiftId
    FROM @Inserted;

    COMMIT TRANSACTION;
END;
