-- database/50_procedures/game/usp_OfflineShop_ExecutePurchase.sql
-- Money-overflow quirk reproduced exactly (verified S07_MyGame09.cpp:805-816): crediting the seller's shop
-- Money past 2,000,000,000 rolls the excess into +2 BigMoney rather than capping -- not "corrected", per D8.
-- All arithmetic is widened to BIGINT before the addition and every CASE WHEN comparison (matching legacy's
-- own widen-then-compare pattern) then CAST back down to the column's INT type -- game.OfflineShops.Money and
-- @Price are both 32-bit INT, and their true sum can reach ~2,999,999,999, past Int32.MaxValue, so evaluating
-- the addition as 32-bit arithmetic first would raise a raw overflow error (8115) instead of the intended
-- graceful rollover.
--
-- Also auto-closes the shop (ShopState 1 -> 0) the instant a purchase empties every one of its listing slots
-- (Server/ts25zone/S07_MyGame09.cpp:860-880), the same routine an explicit owner-close uses -- this only
-- fires on the "someone bought an item" branch, never on the owner's own "retrieve an unsold item" branch, so
-- usp_OfflineShop_RetrieveItemAndReplaceContainer is untouched. Evaluated across the whole shop (all 25
-- slots), not just the slot that was just sold, and inside this procedure's single transaction so there is no
-- new cross-call TOCTOU window.
CREATE PROCEDURE game.usp_OfflineShop_ExecutePurchase @SellerCharacterId INT,
                                                       @SlotIndex SMALLINT,
                                                       @ExpectedItemId INT,
                                                       @ExpectedQuantity INT,
                                                       @ExpectedValue INT,
                                                       @Price INT,
                                                       @BuyerCharacterId INT,
                                                       @BuyerContainer TINYINT,
                                                       @BuyerItems game.tvp_CharacterItemSlot READONLY
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    BEGIN TRANSACTION;

    DELETE
    FROM game.OfflineShopItems
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

    UPDATE game.OfflineShops
    SET Money    = CAST(CASE
                            WHEN CAST(Money AS BIGINT) + CAST(@Price AS BIGINT) > 2000000000
                                THEN CAST(Money AS BIGINT) + CAST(@Price AS BIGINT) - 2000000000
                            ELSE CAST(Money AS BIGINT) + CAST(@Price AS BIGINT)
        END AS INT),
        BigMoney = CASE
                       WHEN CAST(Money AS BIGINT) + CAST(@Price AS BIGINT) > 2000000000 THEN BigMoney + 2
                       ELSE BigMoney
            END
    WHERE CharacterId = @SellerCharacterId
      AND (CASE
               WHEN CAST(Money AS BIGINT) + CAST(@Price AS BIGINT) > 2000000000 THEN BigMoney + 2
               ELSE BigMoney
        END) <= 999;

    IF @@ROWCOUNT = 0
        THROW 50273, N'Crediting the seller''s offline-shop earnings would exceed the BigMoney cap (999).', 1;

    -- Sellout auto-close: none of the seller's 25 listing slots holds an item any more -- close the shop the
    -- same way an explicit owner-close would (ShopState 1 -> 0), so usp_OfflineShop_WithdrawMoney's
    -- `ShopState = 0` gate no longer waits on rental expiry or a manual close.
    IF NOT EXISTS (SELECT 1 FROM game.OfflineShopItems WHERE CharacterId = @SellerCharacterId)
        UPDATE game.OfflineShops
        SET ShopState = 0
        WHERE CharacterId = @SellerCharacterId
          AND ShopState = 1;

    COMMIT TRANSACTION;
END;
