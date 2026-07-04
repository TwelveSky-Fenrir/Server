-- database/50_procedures/game/usp_Gift_ClaimIntoVault.sql
-- Contract: atomically claim a pending gift (same Status 0->1 guard as usp_Gift_Claim, GiftLog row in the
-- SAME transaction) AND place its item into the account's shared vault (CL_WANT_GIFT_SEND, login protocol
-- report §4.21 -- "moved to the shared chest"). game.AccountVaultItems has a hard FK to game.AccountVault
-- (AccountId), so this ensures that parent row exists first (an account's vault is otherwise only created
-- lazily via usp_AccountVault_EnsureInitialized on vault-panel open -- a gift claim must not require the
-- player to have opened the vault panel first).
-- Params:
--   @GiftId    INT -- game.Gifts.GiftId
--   @AccountId INT -- must own @GiftId (same defense as usp_Gift_Claim)
-- Result set (RS0, single row): SlotIndex SMALLINT -- the vault slot the item landed in (so the caller can
-- report a real inventory position, matching ZC_WANT_GIFT... — no, LC_WANT_GIFT_RECV has no position field
-- today, but callers may still want it for logging).
-- Idempotent: no (same posture as usp_Gift_Claim).
-- Errors:
--   THROW 50220 -- @GiftId does not exist, does not belong to @AccountId, or is not Pending (shared with
--                  usp_Gift_Claim).
--   THROW 50274 -- the account's vault is full (28 slots, MAX_SAVE_ITEM_SLOT_NUM) -- admin.ErrorCatalog,
--                  502xx = game range. The gift stays Pending (not consumed) so the player can free a slot
--                  and retry -- the whole transaction rolls back under XACT_ABORT.
CREATE PROCEDURE game.usp_Gift_ClaimIntoVault @GiftId    INT,
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

    IF
NOT EXISTS (SELECT 1 FROM game.AccountVault WHERE AccountId = @AccountId)
        INSERT INTO game.AccountVault (AccountId) VALUES (@AccountId);

    DECLARE
@FreeSlot SMALLINT;

SELECT TOP(1) @FreeSlot = Slots.n
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
