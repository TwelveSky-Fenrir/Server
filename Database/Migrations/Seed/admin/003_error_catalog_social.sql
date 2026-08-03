IF
    NOT EXISTS (SELECT 1
                FROM admin.ErrorCatalog
                WHERE ErrorNumber = 50267)
    INSERT INTO admin.ErrorCatalog (ErrorNumber, SchemaName, Description)
    VALUES (50267, 'game', N'usp_CharacterFriend_Add: friend slot is already occupied.');

IF
    NOT EXISTS (SELECT 1
                FROM admin.ErrorCatalog
                WHERE ErrorNumber = 50268)
    INSERT INTO admin.ErrorCatalog (ErrorNumber, SchemaName, Description)
    VALUES (50268, 'game',
            N'usp_CharacterTrade_Execute/usp_CharacterTradeCommit_ExecuteIdempotent: Character A -- unknown character or insufficient money balance for this trade.');

IF
    NOT EXISTS (SELECT 1
                FROM admin.ErrorCatalog
                WHERE ErrorNumber = 50269)
    INSERT INTO admin.ErrorCatalog (ErrorNumber, SchemaName, Description)
    VALUES (50269, 'game',
            N'usp_CharacterTrade_Execute/usp_CharacterTradeCommit_ExecuteIdempotent: Character B -- unknown character or insufficient money balance for this trade.');
