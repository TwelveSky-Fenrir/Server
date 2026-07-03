-- database/50_procedures/game/usp_OfflineShop_SetItems.sql
-- Contract: whole-list replace of one character's shop item slots.
-- Params:
--   @CharacterId INT
--   @Items       game.tvp_OfflineShopItemSlot READONLY -- rows: SlotIndex, ItemId (NULL for an empty
--     slot -- translate legacy 0 -> NULL before calling), Quantity, Value, SerialNumber, Price, SocketData
-- Result set: none.
-- Idempotent: yes -- replaying the same @Items list is a no-op in effect.
-- DELETE-then-INSERT, never MERGE (architecture reference §12.3): the client submits its entire shop layout
-- in one shot (Protocol/STRUCT.h's PROXY_SHOP_USER_INFO.tItem array is saved as a whole block, never
-- slot-by-slot -- see game.tvp_OfflineShopItemSlot), so a full-replace is the correct semantics, not a
-- per-row merge.
CREATE PROCEDURE game.usp_OfflineShop_SetItems @CharacterId INT,
    @Items       game.tvp_OfflineShopItemSlot READONLY
AS
BEGIN
    SET
NOCOUNT ON;
    SET
XACT_ABORT ON;

DELETE
FROM game.OfflineShopItems
WHERE CharacterId = @CharacterId;

INSERT INTO game.OfflineShopItems (CharacterId, SlotIndex, ItemId, Quantity, Value, SerialNumber, Price, SocketData)
SELECT @CharacterId,
       SlotIndex,
       ItemId,
       Quantity,
       Value,
       SerialNumber,
       Price,
       SocketData
FROM @Items;
END;
