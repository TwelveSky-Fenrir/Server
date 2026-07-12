-- New file rather than an edit to an earlier error-catalog script: applied scripts are never edited.
-- Backs game.usp_Character_ApplyTribeFourConversion (fourth-tribe/Fujin conversion and return,
-- CZ_CHANGE_TO_TRIBE4_SEND op37).
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
