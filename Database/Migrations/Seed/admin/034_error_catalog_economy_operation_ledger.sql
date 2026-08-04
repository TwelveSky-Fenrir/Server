IF NOT EXISTS (SELECT 1
               FROM admin.ErrorCatalog
               WHERE ErrorNumber = 50366)
    INSERT INTO admin.ErrorCatalog (ErrorNumber, SchemaName, Description)
    VALUES (50366,
            'game',
            N'usp_EconomyOperation_BeginOrRead: the supplied actor character does not belong to the supplied actor account.');

IF NOT EXISTS (SELECT 1
               FROM admin.ErrorCatalog
               WHERE ErrorNumber = 50367)
    INSERT INTO admin.ErrorCatalog (ErrorNumber, SchemaName, Description)
    VALUES (50367,
            'game',
            N'usp_EconomyOperation_Complete: the supplied final status is not terminal.');

IF NOT EXISTS (SELECT 1
               FROM admin.ErrorCatalog
               WHERE ErrorNumber = 50368)
    INSERT INTO admin.ErrorCatalog (ErrorNumber, SchemaName, Description)
    VALUES (50368,
            'game',
            N'usp_EconomyOperation_Complete: no operation exists for the supplied operation identifier and actor account.');
