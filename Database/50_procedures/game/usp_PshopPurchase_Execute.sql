-- database/50_procedures/game/usp_PshopPurchase_Execute.sql
-- Contract: the atomic two-character LIVE personal-shop-stall purchase commit -- CZ_BUY_PSHOP_SEND
-- (contracts/04_commerce.md, verified S04_MyWork02.cpp:6925-7124). Unlike an offline/proxy shop, a live
-- PShop's advertised item never leaves the seller's own inventory container until the moment of sale (the
-- PSHOP_INFO listing only ever POINTS at a live inventory slot) -- so both sides' containers are touched
-- here, the seller's, one-container twin of usp_CharacterTrade_Execute's own two-container/two-character
-- shape. D7 regime (b): both sides' money AND item container commit in ONE transaction, or neither does.
-- Params:
--   @SellerCharacterId      INT
--   @SellerContainer        TINYINT
--   @SellerItems            game.tvp_CharacterItemSlot READONLY -- seller's FULL new container (sold slot removed/decremented)
--   @BuyerCharacterId       INT
--   @BuyerContainer         TINYINT
--   @BuyerItems             game.tvp_CharacterItemSlot READONLY -- buyer's FULL new container (item added)
--   @Price                  INT -- buyer pays, seller receives (both plain Money, never BigMoney -- verified
--                                  S04_MyWork02.cpp:7056/7073, live PShop trades never touch aBigMoney)
-- Result set: none.
-- Idempotent: no.
-- No item-slot CAS guard here (unlike the offline-shop procs): the CALLER (BuyShopItemHandler) has
-- already re-validated the seller's LIVE PlayerRuntimeState.Inventory slot against the advertised listing
-- under BOTH participants' EconomyActionLock (held across this whole call), the same posture
-- TradeLockHandler's own commit already uses -- by the time this proc runs, the projected containers are
-- already known-correct, not merely assumed.
-- Errors:
--   THROW 50222 -- the buyer's money balance is insufficient, or @BuyerCharacterId is unknown (shared with
--                  usp_Character_AdjustMoney).
--   THROW 50261 -- either side's money adjustment would exceed the legacy money cap (shared with
--                  usp_Character_AdjustMoney).
--   THROW 50275 -- @SellerCharacterId is unknown (defensive -- should not happen given the caller's own
--                  live-state check immediately before calling).
CREATE PROCEDURE game.usp_PshopPurchase_Execute @SellerCharacterId INT,
    @SellerContainer   TINYINT,
    @SellerItems       game.tvp_CharacterItemSlot READONLY,
    @BuyerCharacterId  INT,
    @BuyerContainer    TINYINT,
    @BuyerItems        game.tvp_CharacterItemSlot READONLY,
    @Price             INT
AS
BEGIN
    SET
NOCOUNT ON;
    SET
XACT_ABORT ON;

BEGIN
TRANSACTION;

UPDATE game.Characters
SET Money        = Money - @Price,
    UpdatedAtUtc = SYSUTCDATETIME()
WHERE CharacterId = @BuyerCharacterId
  AND Money - @Price BETWEEN 0 AND 2000000000;

IF
@@ROWCOUNT = 0
        THROW 50222, N'Unknown buyer character or insufficient money balance for this purchase.', 1;

UPDATE game.Characters
SET Money        = Money + @Price,
    UpdatedAtUtc = SYSUTCDATETIME()
WHERE CharacterId = @SellerCharacterId
  AND Money + @Price BETWEEN 0 AND 2000000000;

IF
@@ROWCOUNT = 0
        THROW 50275, N'Unknown seller character, or the sale would exceed the legacy money cap.', 1;

DELETE
FROM game.CharacterItems
WHERE CharacterId = @SellerCharacterId
  AND Container = @SellerContainer;
INSERT INTO game.CharacterItems (CharacterId, Container, Slot, ItemId, Quantity, Enchant, Combine,
                                 Refine, Socket, SocketGem1, SocketGem2, SocketGem3, ExpireDate, Serial)
SELECT @SellerCharacterId,
       @SellerContainer,
       Slot,
       ItemId,
       Quantity,
       Enchant,
       Combine,
       Refine,
       Socket,
       SocketGem1,
       SocketGem2,
       SocketGem3,
       ExpireDate,
       Serial
FROM @SellerItems;

DELETE
FROM game.CharacterItems
WHERE CharacterId = @BuyerCharacterId
  AND Container = @BuyerContainer;
INSERT INTO game.CharacterItems (CharacterId, Container, Slot, ItemId, Quantity, Enchant, Combine,
                                 Refine, Socket, SocketGem1, SocketGem2, SocketGem3, ExpireDate, Serial)
SELECT @BuyerCharacterId,
       @BuyerContainer,
       Slot,
       ItemId,
       Quantity,
       Enchant,
       Combine,
       Refine,
       Socket,
       SocketGem1,
       SocketGem2,
       SocketGem3,
       ExpireDate,
       Serial
FROM @BuyerItems;

COMMIT TRANSACTION;
END;
