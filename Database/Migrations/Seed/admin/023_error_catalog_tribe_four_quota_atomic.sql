IF NOT EXISTS (SELECT 1
               FROM admin.ErrorCatalog
               WHERE ErrorNumber = 50355)
    INSERT INTO admin.ErrorCatalog (ErrorNumber, SchemaName, Description)
    VALUES (50355, 'game',
            N'usp_Character_ApplyTribeFourConversion: shared daily fourth-tribe quota exhausted.');
