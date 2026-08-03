IF NOT EXISTS (SELECT 1
               FROM admin.ErrorCatalog
               WHERE ErrorNumber = 50336)
    INSERT INTO admin.ErrorCatalog (ErrorNumber, SchemaName, Description)
    VALUES (50336, 'game',
            N'usp_Character_AdjustZone241Time: unknown character or insufficient Zone241Time balance for this adjustment.');
