IF NOT EXISTS (SELECT 1
               FROM admin.ErrorCatalog
               WHERE ErrorNumber = 50334)
    INSERT INTO admin.ErrorCatalog (ErrorNumber, SchemaName, Description)
    VALUES (50334, 'game', N'usp_Character_ApplyTribeFourConversion: unknown CharacterId.');

IF NOT EXISTS (SELECT 1
               FROM admin.ErrorCatalog
               WHERE ErrorNumber = 50335)
    INSERT INTO admin.ErrorCatalog (ErrorNumber, SchemaName, Description)
    VALUES (50335, 'game', N'usp_Character_ApplyTribeFourConversion: NewTribe is outside the legal 0-3 range.');
