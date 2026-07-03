-- database/50_procedures/game/usp_AccountVault_SetItems.sql
-- Contract: whole-list replace of one account's vault item slots.
-- Params:
--   @AccountId INT
--   @Items     game.tvp_AccountVaultItemSlot READONLY -- rows: SlotIndex, ItemId (NULL for an empty slot --
--     translate legacy 0 -> NULL before calling), Quantity, Value, SerialNumber, SocketData
-- Result set: none.
-- Idempotent: yes -- replaying the same @Items list is a no-op in effect.
-- DELETE-then-INSERT, never MERGE (architecture reference §12.3): the legacy uSaveItem/uSaveSocketGem
-- arrays are saved as a whole block, never slot-by-slot, so a full-replace is the correct semantics.
CREATE PROCEDURE game.usp_AccountVault_SetItems @AccountId INT,
    @Items     game.tvp_AccountVaultItemSlot READONLY
AS
BEGIN
    SET
NOCOUNT ON;
    SET
XACT_ABORT ON;

DELETE
FROM game.AccountVaultItems
WHERE AccountId = @AccountId;

INSERT INTO game.AccountVaultItems (AccountId, SlotIndex, ItemId, Quantity, Value, SerialNumber, SocketData)
SELECT @AccountId, SlotIndex, ItemId, Quantity, Value, SerialNumber, SocketData
FROM @Items;
END;
