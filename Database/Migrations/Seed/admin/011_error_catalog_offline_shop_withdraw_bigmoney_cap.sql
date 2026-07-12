-- New file rather than an edit to an earlier error-catalog script: applied scripts are never edited.
-- Registers 50333 for the BigMoney-upper-bound guard added to usp_OfflineShop_WithdrawMoney by
-- Migrations/038_offline_shop_withdraw_bigmoney_cap.sql (proxy-shop-listing-pricing finding).
IF NOT EXISTS (SELECT 1
               FROM admin.ErrorCatalog
               WHERE ErrorNumber = 50333)
    INSERT INTO admin.ErrorCatalog (ErrorNumber, SchemaName, Description)
    VALUES (50333, 'game',
            N'usp_OfflineShop_WithdrawMoney: crediting the character''s own BigMoney with this offline shop''s earnings would exceed the legacy BigMoney cap (MAX_NUMBER_SIZE2 = 999).');
