-- database/50_procedures/game/usp_OfflineShop_RetrieveItemAndReplaceContainer.sql
-- Contract: atomically pull ONE unsold item out of the owner's OWN closed offline shop back into their
-- live inventory -- CZ_SET_DEPUTY_PSHOP_SEND BuySort=RETRIEVED (contracts/04_commerce.md; verified
-- S07_MyGame09.cpp:627-844: retrieval requires ShopState=0/closed, the mirror image of a purchase which
-- requires ShopState=1/open). D7 regime (b).
-- Params:
--   @CharacterId      INT
--   @SlotIndex        SMALLINT -- 0-24, the offline-shop slot being retrieved
--   @ExpectedItemId   INT
--   @ExpectedQuantity INT
--   @ExpectedValue    INT      -- CAS guard: the slot must still hold exactly this (id, quantity, value)
--   @Container        TINYINT  -- the owner's OWN inventory container the item lands in
--   @Items            game.tvp_CharacterItemSlot READONLY -- that container's FULL new content
-- Result set: none.
-- Idempotent: no.
-- Single guarded DELETE ... OUTPUT (CAS): the shop must be closed (ShopState=0) AND the slot must still
-- match every expected value, checked in the SAME statement that removes it -- no read/write race window.
-- Errors:
--   THROW 50272 -- the shop is not closed, or the slot no longer matches (already retrieved/sold, or
--                  stale client state).
CREATE PROCEDURE game.usp_OfflineShop_RetrieveItemAndReplaceContainer
    @CharacterId      INT,
    @SlotIndex        SMALLINT,
    @ExpectedItemId   INT,
    @ExpectedQuantity INT,
    @ExpectedValue    INT,
    @Container        TINYINT,
    @Items            game.tvp_CharacterItemSlot READONLY
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    BEGIN TRANSACTION;

    DELETE FROM game.OfflineShopItems
    WHERE CharacterId = @CharacterId
      AND SlotIndex = @SlotIndex
      AND ItemId = @ExpectedItemId
      AND Quantity = @ExpectedQuantity
      AND Value = @ExpectedValue
      AND EXISTS (SELECT 1 FROM game.OfflineShops WHERE CharacterId = @CharacterId AND ShopState = 0);

    IF @@ROWCOUNT = 0
        THROW 50272, N'Offline shop is not closed, or the slot no longer matches the expected item.', 1;

    DELETE FROM game.CharacterItems WHERE CharacterId = @CharacterId AND Container = @Container;
    INSERT INTO game.CharacterItems (CharacterId, Container, Slot, ItemId, Quantity, Enchant, Combine,
                                      Refine, Socket, SocketGem1, SocketGem2, SocketGem3, ExpireDate, Serial)
    SELECT @CharacterId, @Container, Slot, ItemId, Quantity, Enchant, Combine, Refine, Socket,
           SocketGem1, SocketGem2, SocketGem3, ExpireDate, Serial
    FROM @Items;

    COMMIT TRANSACTION;
END;
