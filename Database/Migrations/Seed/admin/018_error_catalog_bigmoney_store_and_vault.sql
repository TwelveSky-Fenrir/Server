IF NOT EXISTS (SELECT 1
               FROM admin.ErrorCatalog
               WHERE ErrorNumber = 50349)
    INSERT INTO admin.ErrorCatalog (ErrorNumber, SchemaName, Description)
    VALUES (50349, 'game',
            N'usp_Character_AdjustBigStoreMoney: unknown character or insufficient/over-cap balance for this BigMoney/BigStoreMoney adjustment (999-unit cap, tSort 241/244).');

IF NOT EXISTS (SELECT 1
               FROM admin.ErrorCatalog
               WHERE ErrorNumber = 50353)
    INSERT INTO admin.ErrorCatalog (ErrorNumber, SchemaName, Description)
    VALUES (50353, 'game',
            N'usp_AccountVault_TransferBigMoneyWithCharacter: unknown character or insufficient/over-cap BigMoney balance on the character side of this vault transfer (999-unit cap, tSort 242/245).');

IF NOT EXISTS (SELECT 1
               FROM admin.ErrorCatalog
               WHERE ErrorNumber = 50354)
    INSERT INTO admin.ErrorCatalog (ErrorNumber, SchemaName, Description)
    VALUES (50354, 'game',
            N'usp_AccountVault_TransferBigMoneyWithCharacter: insufficient/over-cap account vault BigMoney balance for this transfer (999-unit cap, tSort 242/245).');

