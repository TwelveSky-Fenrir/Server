-- New file rather than an edit to 002_error_catalog_a3.sql: applied scripts are never edited.
--
-- 50268/50269 descriptions refreshed 2026-07-12 (error-catalog-completeness hardening -- see
-- admin.ErrorCatalog's own "no true error-code collisions found" audit): usp_CharacterTradeCommit_ExecuteIdempotent
-- (the C8 idempotent trade-commit variant) THROWs these identical two codes by explicit design (see that
-- procedure's own header comment) but was never added to either row's proc list. Edited in place per this
-- campaign's explicit authorization to correct already-applied Migrations/ scripts rather than layering a new
-- file on top for a pure Description-text refresh -- same convention the concurrent 50261/50222 fix in
-- 002_error_catalog_a3.sql already used this session.
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
