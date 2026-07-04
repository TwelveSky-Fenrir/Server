-- Documents the THROW codes introduced by the phase C/V6 Social procedures (friends, trade), per
-- admin.ErrorCatalog's "documentation as data" contract (architecture reference §12.3). New file, not an
-- edit to 002_error_catalog_a3.sql -- same "never edit an applied script" rule as every other schema
-- addition in this lot (_manifest.txt header).
IF
NOT EXISTS (SELECT 1 FROM admin.ErrorCatalog WHERE ErrorNumber = 50267)
    INSERT INTO admin.ErrorCatalog (ErrorNumber, SchemaName, Description)
    VALUES (50267, N'game', N'usp_CharacterFriend_Add: friend slot is already occupied.');

IF
NOT EXISTS (SELECT 1 FROM admin.ErrorCatalog WHERE ErrorNumber = 50268)
    INSERT INTO admin.ErrorCatalog (ErrorNumber, SchemaName, Description)
    VALUES (50268, N'game', N'usp_CharacterTrade_Execute: Character A -- unknown character or insufficient money balance for this trade.');

IF
NOT EXISTS (SELECT 1 FROM admin.ErrorCatalog WHERE ErrorNumber = 50269)
    INSERT INTO admin.ErrorCatalog (ErrorNumber, SchemaName, Description)
    VALUES (50269, N'game', N'usp_CharacterTrade_Execute: Character B -- unknown character or insufficient money balance for this trade.');
