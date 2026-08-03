IF NOT EXISTS (SELECT 1
               FROM admin.ErrorCatalog
               WHERE ErrorNumber = 50341)
    INSERT INTO admin.ErrorCatalog (ErrorNumber, SchemaName, Description)
    VALUES (50341, 'game',
            N'usp_Character_ApplyTribeScrollConversion: ItemId is not one of the two faction-transfer scrolls (8153/8154).');

IF NOT EXISTS (SELECT 1
               FROM admin.ErrorCatalog
               WHERE ErrorNumber = 50342)
    INSERT INTO admin.ErrorCatalog (ErrorNumber, SchemaName, Description)
    VALUES (50342, 'game',
            N'usp_Character_ApplyTribeScrollConversion: client-supplied target tribe is outside the playable 0..2 range (hardening of the legacy inert range guard).');

IF NOT EXISTS (SELECT 1
               FROM admin.ErrorCatalog
               WHERE ErrorNumber = 50343)
    INSERT INTO admin.ErrorCatalog (ErrorNumber, SchemaName, Description)
    VALUES (50343, 'game', N'usp_Character_ApplyTribeScrollConversion: unknown CharacterId.');

IF NOT EXISTS (SELECT 1
               FROM admin.ErrorCatalog
               WHERE ErrorNumber = 50344)
    INSERT INTO admin.ErrorCatalog (ErrorNumber, SchemaName, Description)
    VALUES (50344, 'game',
            N'usp_Character_ApplyTribeScrollConversion: target tribe already matches this character''s previous tribe (unconditional -- no neutral exemption, unlike the book path).');

IF NOT EXISTS (SELECT 1
               FROM admin.ErrorCatalog
               WHERE ErrorNumber = 50345)
    INSERT INTO admin.ErrorCatalog (ErrorNumber, SchemaName, Description)
    VALUES (50345, 'game',
            N'usp_Character_ApplyTribeScrollConversion: first-level is below the required LV_M33 (145 / max level).');

IF NOT EXISTS (SELECT 1
               FROM admin.ErrorCatalog
               WHERE ErrorNumber = 50346)
    INSERT INTO admin.ErrorCatalog (ErrorNumber, SchemaName, Description)
    VALUES (50346, 'game',
            N'usp_Character_ApplyTribeScrollConversion: character holds a tribe office (master, sub-master, or elected council seat) in its current tribe.');

IF NOT EXISTS (SELECT 1
               FROM admin.ErrorCatalog
               WHERE ErrorNumber = 50347)
    INSERT INTO admin.ErrorCatalog (ErrorNumber, SchemaName, Description)
    VALUES (50347, 'game', N'usp_Character_ApplyTribeScrollConversion: character belongs to a guild.');

IF NOT EXISTS (SELECT 1
               FROM admin.ErrorCatalog
               WHERE ErrorNumber = 50348)
    INSERT INTO admin.ErrorCatalog (ErrorNumber, SchemaName, Description)
    VALUES (50348, 'game', N'usp_Character_ApplyTribeScrollConversion: character has one or more registered friends.');
