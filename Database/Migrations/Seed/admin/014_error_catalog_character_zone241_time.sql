-- New file rather than an edit to an earlier error-catalog script: applied scripts are never edited.
-- Registers 50336 for game.usp_Character_AdjustZone241Time (game.Characters.Zone241Time delta-adjustment
-- primitive -- see Tables/game/Characters.sql's own column comment and the Rebirth-advancement
-- behavior contract's Path B "aZone241Time += 10" finding).
IF NOT EXISTS (SELECT 1
               FROM admin.ErrorCatalog
               WHERE ErrorNumber = 50336)
    INSERT INTO admin.ErrorCatalog (ErrorNumber, SchemaName, Description)
    VALUES (50336, 'game',
            N'usp_Character_AdjustZone241Time: unknown character or insufficient Zone241Time balance for this adjustment.');
