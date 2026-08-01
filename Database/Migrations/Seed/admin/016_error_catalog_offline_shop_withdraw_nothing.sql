-- New file rather than an edit to an earlier error-catalog script: applied scripts are never edited.
-- Registers 50340 for the new "nothing to withdraw" case split out of usp_OfflineShop_WithdrawMoney's
-- previously-shared 50276 by Migrations/026_offline_shop_withdraw_split_nothing_and_expiry.sql
-- (shop-and-proxyshop legacy-parity audit finding).
IF NOT EXISTS (SELECT 1
               FROM admin.ErrorCatalog
               WHERE ErrorNumber = 50340)
    INSERT INTO admin.ErrorCatalog (ErrorNumber, SchemaName, Description)
    VALUES (50340, 'game',
            N'usp_OfflineShop_WithdrawMoney: nothing to withdraw (both Money and BigMoney pending amounts are zero).');

-- 50276 no longer covers the zero-balance case (see 50340 above); refreshes its catalog description to
-- match the split. UPDATE, not a guarded INSERT, since the row already exists from
-- Migrations/Seed/admin/004_error_catalog_v8_commerce.sql -- idempotent by construction (re-running this
-- statement is a no-op once applied).
UPDATE admin.ErrorCatalog
SET Description = N'usp_OfflineShop_WithdrawMoney: offline shop is not closed, has expired, or its earnings no longer match the expected amounts.'
WHERE ErrorNumber = 50276;
