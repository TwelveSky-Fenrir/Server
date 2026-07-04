-- Documents the THROW codes introduced by the phase C/V8 Player Commerce & Cash procedures (daily reward,
-- blood mark, offline/proxy shop, live PShop purchase, gift-into-vault), per admin.ErrorCatalog's
-- "documentation as data" contract (architecture reference §12.3). New file, not an edit to
-- 002_error_catalog_a3.sql/003_error_catalog_social.sql -- same "never edit an applied script" rule.
IF
NOT EXISTS (SELECT 1 FROM admin.ErrorCatalog WHERE ErrorNumber = 50270)
    INSERT INTO admin.ErrorCatalog (ErrorNumber, SchemaName, Description)
    VALUES (50270, N'game', N'usp_Character_ClaimDailyReward: already claimed today, fully claimed, or unknown character.');

IF
NOT EXISTS (SELECT 1 FROM admin.ErrorCatalog WHERE ErrorNumber = 50271)
    INSERT INTO admin.ErrorCatalog (ErrorNumber, SchemaName, Description)
    VALUES (50271, N'game', N'usp_Character_SpendBloodCoinAndReplaceContainer: unknown character or insufficient BloodCoin balance.');

IF
NOT EXISTS (SELECT 1 FROM admin.ErrorCatalog WHERE ErrorNumber = 50272)
    INSERT INTO admin.ErrorCatalog (ErrorNumber, SchemaName, Description)
    VALUES (50272, N'game', N'usp_OfflineShop_OpenAndReplaceContainers/RetrieveItemAndReplaceContainer/ExecutePurchase: offline-shop state/item no longer matches the expected precondition.');

IF
NOT EXISTS (SELECT 1 FROM admin.ErrorCatalog WHERE ErrorNumber = 50273)
    INSERT INTO admin.ErrorCatalog (ErrorNumber, SchemaName, Description)
    VALUES (50273, N'game', N'usp_OfflineShop_ExecutePurchase: crediting the seller''s offline-shop earnings would exceed the BigMoney cap (999).');

IF
NOT EXISTS (SELECT 1 FROM admin.ErrorCatalog WHERE ErrorNumber = 50274)
    INSERT INTO admin.ErrorCatalog (ErrorNumber, SchemaName, Description)
    VALUES (50274, N'game', N'usp_Gift_ClaimIntoVault: account vault is full (28 slots).');

IF
NOT EXISTS (SELECT 1 FROM admin.ErrorCatalog WHERE ErrorNumber = 50275)
    INSERT INTO admin.ErrorCatalog (ErrorNumber, SchemaName, Description)
    VALUES (50275, N'game', N'usp_PshopPurchase_Execute: unknown seller character, or the sale would exceed the legacy money cap.');

IF
NOT EXISTS (SELECT 1 FROM admin.ErrorCatalog WHERE ErrorNumber = 50276)
    INSERT INTO admin.ErrorCatalog (ErrorNumber, SchemaName, Description)
    VALUES (50276, N'game', N'usp_OfflineShop_WithdrawMoney: nothing to withdraw, shop not closed, or earnings no longer match.');

IF
NOT EXISTS (SELECT 1 FROM admin.ErrorCatalog WHERE ErrorNumber = 50220)
    INSERT INTO admin.ErrorCatalog (ErrorNumber, SchemaName, Description)
    VALUES (50220, N'game', N'usp_Gift_Claim/usp_Gift_ClaimIntoVault: gift not found, not owned by this account, or already claimed.');
