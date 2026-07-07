-- Corrective fix for game.usp_OfflineShop_WithdrawMoney (never edited in place -- DbMigrator journals every
-- script by content hash and refuses a changed re-apply).
--
-- Bug (legacy-behavior-translator contract, area proxy-shop-listing-pricing, severity major):
-- Legacy rejects a proxy/offline-shop money withdrawal outright (SET_DEPUTY_PSHOP_MONEY_RECV result code 2,
-- no mutation at all) when crediting the shop's accumulated BigMoney into the character's own aBigMoney
-- would push it past MAX_NUMBER_SIZE2 (999) -- Server/ts25zone/S07_MyGame09.cpp:906-912, constant defined at
-- Server/Header/Protocol/DEFINE.h:367. The guarded UPDATE against game.Characters only enforced
-- `BigMoney + @ExpectedBigMoney >= 0` (non-negativity), never an upper bound, so repeated
-- list/sell/withdraw cycles could push a character's own game.Characters.BigMoney arbitrarily above 999 even
-- though every individual shop-side accumulation is itself already capped at 999 by
-- usp_OfflineShop_ExecutePurchase (game.OfflineShops.BigMoney).
--
-- Fix: fold an inclusive upper bound (`BETWEEN 0 AND 999`) into the same guarded UPDATE that already bounds
-- Money, closing the same TOCTOU window the Money bound already closes for concurrent withdrawals. On
-- rejection this is a clean no-op -- XACT_ABORT rolls back the whole transaction, including the earlier
-- game.OfflineShops zeroing UPDATE in this same batch, so the shop's earnings are never cleared without
-- being credited. The boundary is inclusive per the contract: a resulting BigMoney of exactly 999 is
-- allowed, only a value strictly greater than 999 is rejected -- matching the cap convention already used by
-- usp_OfflineShop_ExecutePurchase's own `<= 999` guard and
-- Application/Fenrir.Application.Game.Domain/Inventory/BigMoneyTransferPolicy.cs's
-- `projectedDestination > BigMoneyCap` rejection (BigMoneyCap = 999).
--
-- A diagnostic re-read after the failed UPDATE (never load-bearing, only picks which error number to throw)
-- distinguishes the new BigMoney-cap rejection from the pre-existing Money-cap/unknown-character case, the
-- same pattern already used by usp_Character_AdjustMoneyAndReplaceTwoContainers.
CREATE OR ALTER PROCEDURE game.usp_OfflineShop_WithdrawMoney @CharacterId INT,
                                                             @ExpectedMoney INT,
                                                             @ExpectedBigMoney INT
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    IF @ExpectedMoney = 0 AND @ExpectedBigMoney = 0
        THROW 50276, N'Nothing to withdraw from this offline shop.', 1;

    BEGIN TRANSACTION;

    UPDATE game.OfflineShops
    SET Money    = 0,
        BigMoney = 0
    WHERE CharacterId = @CharacterId
      AND ShopState = 0
      AND Money = @ExpectedMoney
      AND BigMoney = @ExpectedBigMoney;

    IF @@ROWCOUNT = 0
        THROW 50276, N'Offline shop is not closed, or its earnings no longer match the expected amounts.', 1;

    UPDATE game.Characters
    SET Money        = Money + @ExpectedMoney,
        BigMoney     = BigMoney + @ExpectedBigMoney,
        UpdatedAtUtc = SYSUTCDATETIME()
    WHERE CharacterId = @CharacterId
      AND Money + @ExpectedMoney BETWEEN 0 AND 2000000000
      AND BigMoney + @ExpectedBigMoney BETWEEN 0 AND 999;

    IF @@ROWCOUNT = 0
        BEGIN
            -- Diagnostic re-read only; picks which error code to throw.
            IF EXISTS (SELECT 1
                       FROM game.Characters
                       WHERE CharacterId = @CharacterId
                         AND BigMoney + @ExpectedBigMoney > 999)
                THROW 50333, N'Crediting this offline shop''s BigMoney earnings would exceed the legacy BigMoney cap (MAX_NUMBER_SIZE2 = 999).', 1;

            THROW 50261, N'Withdrawal would exceed the legacy money cap (MAX_NUMBER_SIZE = 2,000,000,000).', 1;
        END;

    COMMIT TRANSACTION;
END;
GO
