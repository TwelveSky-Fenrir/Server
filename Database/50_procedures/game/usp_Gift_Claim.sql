-- Idempotent: no -- claiming an already-claimed gift throws (50220) rather than silently no-op-ing.
CREATE PROCEDURE game.usp_Gift_Claim @GiftId    INT,
    @AccountId INT
AS
BEGIN
    SET
NOCOUNT ON;
    SET
XACT_ABORT ON;

    DECLARE
@Claimed TABLE (ProductId INT NULL, Quantity INT, Value INT);

UPDATE game.Gifts
SET Status = 1 OUTPUT INSERTED.ProductId, INSERTED.Quantity, INSERTED.Value
INTO @Claimed
WHERE GiftId = @GiftId
  AND AccountId = @AccountId
  AND Status = 0;

IF
@@ROWCOUNT = 0
        THROW 50220, N'Gift not found, not owned by this account, or already claimed.', 1;

INSERT INTO game.GiftLog (AccountId, ProductId, Quantity, Value, Status, CreatedAtUtc)
SELECT @AccountId, ProductId, Quantity, Value, 1, SYSUTCDATETIME()
FROM @Claimed;
END;
