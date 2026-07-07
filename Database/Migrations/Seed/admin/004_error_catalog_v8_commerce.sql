-- New file rather than an edit to an earlier error-catalog script: applied scripts are never edited.
IF
    NOT EXISTS (SELECT 1
                FROM admin.ErrorCatalog
                WHERE ErrorNumber = 50270)
    INSERT INTO admin.ErrorCatalog (ErrorNumber, SchemaName, Description)
    VALUES (50270, N'game',
            N'usp_Character_ClaimDailyReward: already claimed today, fully claimed, or unknown character.');

IF
    NOT EXISTS (SELECT 1
                FROM admin.ErrorCatalog
                WHERE ErrorNumber = 50271)
    INSERT INTO admin.ErrorCatalog (ErrorNumber, SchemaName, Description)
    VALUES (50271, N'game',
            N'usp_Character_SpendBloodCoinAndReplaceContainer: unknown character or insufficient BloodCoin balance.');

IF
    NOT EXISTS (SELECT 1
                FROM admin.ErrorCatalog
                WHERE ErrorNumber = 50272)
    INSERT INTO admin.ErrorCatalog (ErrorNumber, SchemaName, Description)
    VALUES (50272, N'game',
            N'usp_OfflineShop_OpenAndReplaceContainers/RetrieveItemAndReplaceContainer/ExecutePurchase: offline-shop state/item no longer matches the expected precondition.');

IF
    NOT EXISTS (SELECT 1
                FROM admin.ErrorCatalog
                WHERE ErrorNumber = 50273)
    INSERT INTO admin.ErrorCatalog (ErrorNumber, SchemaName, Description)
    VALUES (50273, N'game',
            N'usp_OfflineShop_ExecutePurchase: crediting the seller''s offline-shop earnings would exceed the BigMoney cap (999).');

IF
    NOT EXISTS (SELECT 1
                FROM admin.ErrorCatalog
                WHERE ErrorNumber = 50274)
    INSERT INTO admin.ErrorCatalog (ErrorNumber, SchemaName, Description)
    VALUES (50274, N'game', N'usp_Gift_ClaimIntoVault: account vault is full (28 slots).');

IF
    NOT EXISTS (SELECT 1
                FROM admin.ErrorCatalog
                WHERE ErrorNumber = 50275)
    INSERT INTO admin.ErrorCatalog (ErrorNumber, SchemaName, Description)
    VALUES (50275, N'game',
            N'usp_PshopPurchase_Execute: unknown seller character, or the sale would exceed the legacy money cap.');

IF
    NOT EXISTS (SELECT 1
                FROM admin.ErrorCatalog
                WHERE ErrorNumber = 50276)
    INSERT INTO admin.ErrorCatalog (ErrorNumber, SchemaName, Description)
    VALUES (50276, N'game',
            N'usp_OfflineShop_WithdrawMoney: nothing to withdraw, shop not closed, or earnings no longer match.');

IF
    NOT EXISTS (SELECT 1
                FROM admin.ErrorCatalog
                WHERE ErrorNumber = 50220)
    INSERT INTO admin.ErrorCatalog (ErrorNumber, SchemaName, Description)
    VALUES (50220, N'game',
            N'usp_Gift_Claim/usp_Gift_ClaimIntoVault: gift not found, not owned by this account, or already claimed.');
