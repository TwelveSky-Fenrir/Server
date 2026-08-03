IF NOT EXISTS (SELECT 1
               FROM admin.ErrorCatalog
               WHERE ErrorNumber = 50340)
    INSERT INTO admin.ErrorCatalog (ErrorNumber, SchemaName, Description)
    VALUES (50340, 'game',
            N'usp_OfflineShop_WithdrawMoney: nothing to withdraw (both Money and BigMoney pending amounts are zero).');

UPDATE admin.ErrorCatalog
SET Description = N'usp_OfflineShop_WithdrawMoney: offline shop is not closed, has expired, or its earnings no longer match the expected amounts.'
WHERE ErrorNumber = 50276;
