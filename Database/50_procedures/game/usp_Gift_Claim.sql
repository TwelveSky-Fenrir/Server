-- database/50_procedures/game/usp_Gift_Claim.sql
-- Contract: claim one pending gift, flipping Status 0 (Pending) -> 1 (Claimed).
-- Params:
--   @GiftId    INT -- game.Gifts.GiftId
--   @AccountId INT -- auth.Accounts.AccountId; must own @GiftId (defense against claiming another
--                     account's gift by guessing an id)
-- Result set: none.
-- Idempotent: no (a second call for an already-claimed gift throws rather than silently no-op-ing, so the
-- caller cannot mistake "already claimed by someone else / race lost" for "claimed just now").
-- Single guarded UPDATE ... OUTPUT (not a separate SELECT-then-UPDATE): atomically claims the row and
-- captures its pre-claim values in the same statement, so there is no read/write race window to lock
-- against (no explicit transaction needed, matching this repo's usual XACT_ABORT+THROW idiom). Appends a
-- GiftLog row capturing the post-claim state, so the log has an entry for every state a gift ever passed
-- through.
-- Errors:
--   THROW 50220 -- @GiftId does not exist, does not belong to @AccountId, or is not Pending
--                  (admin.ErrorCatalog, 502xx = game range).
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
