-- New error code for game.usp_Character_AdjustDeathProtection (area character-death-respawn-penalty):
-- registers the next free number in the game 502xx range. 50330/50331 are already THROWn by
-- usp_Tribe_SetMaster but were never catalogued here -- a pre-existing gap, out of this finding's scope,
-- deliberately left untouched.
IF NOT EXISTS (SELECT 1
               FROM admin.ErrorCatalog
               WHERE ErrorNumber = 50332)
    INSERT INTO admin.ErrorCatalog (ErrorNumber, SchemaName, Description)
    VALUES (50332, N'game',
            N'usp_Character_AdjustDeathProtection: unknown character or insufficient ProtectForDeath balance for this adjustment.');
