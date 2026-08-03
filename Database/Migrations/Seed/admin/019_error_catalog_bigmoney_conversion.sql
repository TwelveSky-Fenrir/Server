IF NOT EXISTS (SELECT 1
               FROM admin.ErrorCatalog
               WHERE ErrorNumber = 50352)
    INSERT INTO admin.ErrorCatalog (ErrorNumber, SchemaName, Description)
    VALUES (50352, 'game',
            N'usp_Character_AdjustBigMoneyConversion: unknown character or insufficient/over-cap balance for this Money/BigMoney conversion adjustment (tSort 246/247).');
