IF NOT EXISTS (SELECT 1
               FROM admin.ErrorCatalog
               WHERE ErrorNumber = 50369)
    INSERT INTO admin.ErrorCatalog (ErrorNumber, SchemaName, Description)
    VALUES (50369,
            'game',
            N'usp_MonsterMoneyGrant_ApplyIdempotent: correlation identifier, character identifier, or credit amount is outside the permitted bounds.');

IF NOT EXISTS (SELECT 1
               FROM admin.ErrorCatalog
               WHERE ErrorNumber = 50370)
    INSERT INTO admin.ErrorCatalog (ErrorNumber, SchemaName, Description)
    VALUES (50370,
            'game',
            N'usp_MonsterMoneyGrant_ApplyIdempotent: a correlation identifier was replayed with a different character or amount.');

IF NOT EXISTS (SELECT 1
               FROM admin.ErrorCatalog
               WHERE ErrorNumber = 50371)
    INSERT INTO admin.ErrorCatalog (ErrorNumber, SchemaName, Description)
    VALUES (50371,
            'game',
            N'usp_MonsterMoneyGrant_ApplyIdempotent: character does not exist.');

IF NOT EXISTS (SELECT 1
               FROM admin.ErrorCatalog
               WHERE ErrorNumber = 50372)
    INSERT INTO admin.ErrorCatalog (ErrorNumber, SchemaName, Description)
    VALUES (50372,
            'game',
            N'usp_MonsterMoneyGrant_ApplyIdempotent: credit would exceed the configured money cap (2,000,000,000).');
