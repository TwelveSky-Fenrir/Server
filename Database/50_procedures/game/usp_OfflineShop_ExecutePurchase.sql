-- database/50_procedures/game/usp_OfflineShop_ExecutePurchase.sql
-- Contract: the atomic offline-shop purchase commit -- CZ_SET_DEPUTY_PSHOP_SEND BuySort=PURCHASED
-- (contracts/04_commerce.md, verified S07_MyGame09.cpp:604-857). The buyer is necessarily online (D7
-- regime (b): their money debit + item grant commit synchronously here); the SELLER need not be --
-- proceeds accumulate on game.OfflineShops.Money/BigMoney for later withdrawal
-- (usp_OfflineShop_WithdrawMoney), never touching the seller's live game.Characters.Money directly, so no
-- seller PlayerRuntimeState mirror is ever required by this action.
-- Params:
--   @SellerCharacterId INT
--   @SlotIndex         SMALLINT
--   @ExpectedItemId    INT
--   @ExpectedQuantity  INT
--   @ExpectedValue     INT
--   @Price             INT      -- CAS guard: the slot must still match every one of these AND the shop
--                                   must still be open (ShopState=1)
--   @BuyerCharacterId  INT
--   @BuyerContainer    TINYINT
--   @BuyerItems        game.tvp_CharacterItemSlot READONLY -- buyer's FULL new container content
-- Result set: none.
-- Idempotent: no.
-- Money-overflow quirk verified byte-for-bit (S07_MyGame09.cpp:805-816): crediting the seller's shop
-- Money past MAX_NUMBER_SIZE (2,000,000,000) rolls 2,000,000,000 off into +2 BigMoney rather than capping
-- -- reproduced exactly, not "corrected", per D8 (preserve exact legacy behavior for anything
-- client-observable). BigMoney itself is capped at MAX_NUMBER_SIZE2 (999, verified DEFINE.h) -- a would-be
-- breach aborts the WHOLE purchase (no partial commit), same as every other guarded UPDATE in this schema.
-- Errors:
--   THROW 50272 -- the seller's slot no longer matches (already sold/retrieved, or stale client state), or
--                  the shop is not open.
--   THROW 50222 -- the buyer's money balance is insufficient, or @BuyerCharacterId is unknown (shared with
--                  usp_Character_AdjustMoney).
--   THROW 50261 -- the buyer's money adjustment would exceed the legacy money cap (shared with
--                  usp_Character_AdjustMoney).
--   THROW 50273 -- crediting the seller's shop earnings would exceed the BigMoney cap (MAX_NUMBER_SIZE2 = 999).
CREATE PROCEDURE game.usp_OfflineShop_ExecutePurchase
    @SellerCharacterId INT,
    @SlotIndex         SMALLINT,
    @ExpectedItemId    INT,
    @ExpectedQuantity  INT,
    @ExpectedValue     INT,
    @Price             INT,
    @BuyerCharacterId  INT,
    @BuyerContainer    TINYINT,
    @BuyerItems        game.tvp_CharacterItemSlot READONLY
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    BEGIN TRANSACTION;

    DELETE FROM game.OfflineShopItems
    WHERE CharacterId = @SellerCharacterId
      AND SlotIndex = @SlotIndex
      AND ItemId = @ExpectedItemId
      AND Quantity = @ExpectedQuantity
      AND Value = @ExpectedValue
      AND Price = @Price
      AND EXISTS (SELECT 1 FROM game.OfflineShops WHERE CharacterId = @SellerCharacterId AND ShopState = 1);

    IF @@ROWCOUNT = 0
        THROW 50272, N'Offline shop item no longer matches the expected listing, or the shop is not open.', 1;

    UPDATE game.Characters
    SET Money        = Money - @Price,
        UpdatedAtUtc = SYSUTCDATETIME()
    WHERE CharacterId = @BuyerCharacterId
      AND Money - @Price BETWEEN 0 AND 2000000000;

    IF @@ROWCOUNT = 0
        THROW 50222, N'Unknown character or insufficient money balance for this purchase.', 1;

    DELETE FROM game.CharacterItems WHERE CharacterId = @BuyerCharacterId AND Container = @BuyerContainer;
    INSERT INTO game.CharacterItems (CharacterId, Container, Slot, ItemId, Quantity, Enchant, Combine,
                                      Refine, Socket, SocketGem1, SocketGem2, SocketGem3, ExpireDate, Serial)
    SELECT @BuyerCharacterId, @BuyerContainer, Slot, ItemId, Quantity, Enchant, Combine, Refine, Socket,
           SocketGem1, SocketGem2, SocketGem3, ExpireDate, Serial
    FROM @BuyerItems;

    UPDATE game.OfflineShops
    SET Money    = CASE WHEN Money + @Price > 2000000000 THEN Money + @Price - 2000000000 ELSE Money + @Price END,
        BigMoney = CASE WHEN Money + @Price > 2000000000 THEN BigMoney + 2 ELSE BigMoney END
    WHERE CharacterId = @SellerCharacterId
      AND (CASE WHEN Money + @Price > 2000000000 THEN BigMoney + 2 ELSE BigMoney END) <= 999;

    IF @@ROWCOUNT = 0
        THROW 50273, N'Crediting the seller''s offline-shop earnings would exceed the BigMoney cap (999).', 1;

    COMMIT TRANSACTION;
END;
