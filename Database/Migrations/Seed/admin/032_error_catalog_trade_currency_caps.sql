IF NOT EXISTS (SELECT 1
               FROM admin.ErrorCatalog
               WHERE ErrorNumber = 50362)
    INSERT INTO admin.ErrorCatalog (ErrorNumber, SchemaName, Description)
    VALUES (50362,
            'game',
            N'usp_CharacterTrade_Execute / usp_CharacterTradeCommit_ExecuteIdempotent: Character A -- crediting this trade would exceed the legacy money cap (MAX_NUMBER_SIZE = 2,000,000,000) or BigMoney cap (MAX_NUMBER_SIZE2 = 999).');

IF NOT EXISTS (SELECT 1
               FROM admin.ErrorCatalog
               WHERE ErrorNumber = 50363)
    INSERT INTO admin.ErrorCatalog (ErrorNumber, SchemaName, Description)
    VALUES (50363,
            'game',
            N'usp_CharacterTrade_Execute / usp_CharacterTradeCommit_ExecuteIdempotent: Character B -- crediting this trade would exceed the legacy money cap (MAX_NUMBER_SIZE = 2,000,000,000) or BigMoney cap (MAX_NUMBER_SIZE2 = 999).');
