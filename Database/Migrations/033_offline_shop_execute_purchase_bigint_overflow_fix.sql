-- Corrective fix for game.usp_OfflineShop_ExecutePurchase (never edited in place -- DbMigrator journals every
-- script by content hash and refuses a changed re-apply).
--
-- Bug (legacy-behavior-translator contract, area proxy-shop-listing-pricing, severity critical):
-- game.OfflineShops.Money and @Price are both 32-bit INT. The seller-earnings-credit UPDATE evaluated
-- `Money + @Price` as 32-bit arithmetic *before* any of the CASE WHEN rollover branches were even chosen.
-- Because OfflineShops.Money can legitimately sit at exactly the rollover ceiling (2,000,000,000, per the
-- CASE's own strict "> 2000000000" threshold) and a single listing's Price can be as large as 999,999,999
-- (PshopPurchasePolicy.MaxSellPrice), the true sum can reach ~2,999,999,999 -- past Int32.MaxValue
-- (2,147,483,647) -- so SQL Server raises "Arithmetic overflow error converting expression to data type
-- int" (error 8115) and the whole purchase (buyer debit + item grant + earnings credit) aborts via
-- XACT_ABORT, instead of legacy's intended graceful BigMoney rollover. Legacy widens to LONGLONG
-- specifically to avoid this (Server/ts25zone/S07_MyGame09.cpp:805-816's
-- `llMoney = (LONGLONG)tempObj.mInfo.tMoney + (LONGLONG)tMoney`), the same widen-then-compare convention
-- used project-wide (Server/Header/function.h:132-140's CheckOverMaximum).
--
-- Fix: widen both operands to BIGINT before the addition and every CASE WHEN comparison, matching legacy's
-- own widen-then-compare pattern, then CAST the stored Money result back down to the column's INT type --
-- safe because the widened sum is bounded above by 2,000,000,000 + 999,999,999 = 2,999,999,999, and every
-- branch's stored result (either the sum itself, when <= 2,000,000,000, or the sum minus 2,000,000,000,
-- when it rolled over) is always <= 2,000,000,000, which fits comfortably inside INT's range. BigMoney's
-- own arithmetic (BigMoney + 2, compared against 999) was never at overflow risk and is unchanged.
--
-- Not touched here: usp_OfflineShop_WithdrawMoney's `Money + @ExpectedMoney` (game.Characters.Money is
-- BIGINT, not INT, so that addition is already evaluated as 64-bit arithmetic by SQL Server's own
-- type-precedence rules and cannot overflow the way this one could -- the originating finding's claim of an
-- equivalent bug there does not hold against the current schema, per the behavior contract).
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

    COMMIT TRANSACTION;
END;
GO
