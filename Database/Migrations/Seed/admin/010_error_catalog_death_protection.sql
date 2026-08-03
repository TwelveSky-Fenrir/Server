IF NOT EXISTS (SELECT 1
               FROM admin.ErrorCatalog
               WHERE ErrorNumber = 50332)
    INSERT INTO admin.ErrorCatalog (ErrorNumber, SchemaName, Description)
    VALUES (50332, 'game',
            N'usp_Character_AdjustDeathProtection: unknown character or insufficient ProtectForDeath balance for this adjustment.');
