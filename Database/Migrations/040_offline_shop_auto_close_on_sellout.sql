-- Corrective fix for game.usp_OfflineShop_ExecutePurchase (never edited in place -- DbMigrator journals every
-- script by content hash and refuses a changed re-apply). Builds on top of the BigInt-overflow widening
-- already shipped in Migrations/033_offline_shop_execute_purchase_bigint_overflow_fix.sql, carried forward
-- unchanged below.
--
-- Bug (legacy-behavior-translator contract, area proxy-shop-listing-pricing, severity major):
-- Legacy auto-closes a proxy/offline shop the instant a purchase against it empties every one of its 25
-- listing slots (5 pages x 5 slots) -- Server/ts25zone/S07_MyGame09.cpp:860-880, closing through the same
-- routine (:220-272) an explicit owner-close uses (production PPSHOP_V2 branch, unconditionally compiled per
-- Server/Header/Protocol/DEFINE.h:95). This auto-close only fires on the "someone bought an item" branch,
-- never on the owner's own "retrieve an unsold item" branch (Server/ts25zone/S07_MyGame09.cpp:801,838,860),
-- so this fix is scoped to usp_OfflineShop_ExecutePurchase only -- usp_OfflineShop_RetrieveItemAndReplaceContainer
-- is untouched.
--
-- game.usp_OfflineShop_ExecutePurchase deleted the sold slot's row but never inspected the seller's remaining
-- row count, so a fully-sold-out shop stayed ShopState=1 (open) indefinitely -- blocking
-- usp_OfflineShop_WithdrawMoney's `ShopState = 0` gate (Database/StoredProcedures/game/usp_OfflineShop_WithdrawMoney.sql)
-- until the owner manually closed it (usp_OfflineShop_SetState, via CloseShopStallService) or the periodic
-- rental-expiry sweep force-closed it (Application/Fenrir.Application.Game.Domain/World/Zone.ProxyShops.cs's
-- RebroadcastProxyShops).
--
-- Fix: after the sold slot's row is deleted -- already inside this procedure's single transaction, atomic
-- with the buyer debit/grant and seller earnings credit, so there is no new cross-call TOCTOU window -- check
-- whether any row remains in game.OfflineShopItems for @SellerCharacterId; if none, flip ShopState to 0
-- (closed) in the same transaction. This is the SQL-only half of legacy's CloseProxyShop: Fenrir has no
-- separate Extra process to notify, so "the cross-process notification must succeed before the local state
-- changes" collapses into "the whole purchase commits or none of it does" under the XACT_ABORT transaction
-- already wrapping this procedure -- there is no separate failure mode to reproduce. The remaining half of
-- legacy's CloseProxyShop -- removing the shop's live broadcast-registry entry
-- (Application/Fenrir.Application.Game.Domain/World/Zone.ProxyShops.cs's _proxyShops) and telling nearby
-- clients the stall despawned (action state 3) -- is an Application-layer concern out of this procedure's
-- scope; no new repository surface is needed for that follow-up, since usp_OfflineShop_GetByCharacter's
-- existing ShopState column already reflects the closure performed here.
--
-- Edge case: emptiness is evaluated across the whole shop (all 25 slots), not just the slot that was just
-- sold -- a shop with 1 remaining listed item is not closed (Server/ts25zone/S07_MyGame09.cpp:863-874,
-- MAX_PSHOP_PAGE_NUM=5/MAX_PSHOP_SLOT_NUM=5 at Server/Header/Protocol/DEFINE.h:321-322). The
-- `NOT EXISTS (...)` check below reads game.OfflineShopItems after the DELETE above, so it already reflects
-- the just-completed removal.
CREATE OR ALTER PROCEDURE game.usp_OfflineShop_ExecutePurchase @SellerCharacterId INT,
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

    -- Sellout auto-close (this migration's fix): none of the seller's 25 listing slots holds an item any
    -- more -- close the shop the same way an explicit owner-close would (ShopState 1 -> 0), so
    -- usp_OfflineShop_WithdrawMoney's `ShopState = 0` gate no longer waits on rental expiry or a manual close.
    IF NOT EXISTS (SELECT 1 FROM game.OfflineShopItems WHERE CharacterId = @SellerCharacterId)
        UPDATE game.OfflineShops
        SET ShopState = 0
        WHERE CharacterId = @SellerCharacterId
          AND ShopState = 1;

    COMMIT TRANSACTION;
END;
GO
