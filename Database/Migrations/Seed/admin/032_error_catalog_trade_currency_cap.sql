IF NOT EXISTS (SELECT 1
               FROM admin.ErrorCatalog
               WHERE ErrorNumber = 50362)
    INSERT INTO admin.ErrorCatalog (ErrorNumber, SchemaName, Description)
    VALUES (50362,
            'game',
            N'usp_CharacterTrade_Execute / usp_CharacterTradeCommit_ExecuteIdempotent: character A would exceed the legacy money cap (2,000,000,000) or BigMoney cap (999) after this trade.');

IF NOT EXISTS (SELECT 1
               FROM admin.ErrorCatalog
               WHERE ErrorNumber = 50363)
    INSERT INTO admin.ErrorCatalog (ErrorNumber, SchemaName, Description)
    VALUES (50363,
            'game',
            N'usp_CharacterTrade_Execute / usp_CharacterTradeCommit_ExecuteIdempotent: character B would exceed the legacy money cap (2,000,000,000) or BigMoney cap (999) after this trade.');
