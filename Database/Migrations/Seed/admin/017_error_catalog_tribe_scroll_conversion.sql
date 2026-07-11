-- New file rather than an edit to an earlier error-catalog script: applied scripts are never edited.
-- Backs game.usp_Character_ApplyTribeScrollConversion (faction-transfer scroll, world.Items 8153/8154 --
-- TChangeTribe reclassifier). One distinct code per precondition/gate, matching the behavior contract's own
-- scroll gate list. Distinct block from 50313-50320 (the Book-of-* tribe-conversion mechanic) even though the
-- two procedures share equivalence data -- they are genuinely different reclassifiers and the app layer
-- switches on the number.
IF NOT EXISTS (SELECT 1
               FROM admin.ErrorCatalog
               WHERE ErrorNumber = 50341)
    INSERT INTO admin.ErrorCatalog (ErrorNumber, SchemaName, Description)
    VALUES (50341, N'game',
            N'usp_Character_ApplyTribeScrollConversion: ItemId is not one of the two faction-transfer scrolls (8153/8154).');

IF NOT EXISTS (SELECT 1
               FROM admin.ErrorCatalog
               WHERE ErrorNumber = 50342)
    INSERT INTO admin.ErrorCatalog (ErrorNumber, SchemaName, Description)
    VALUES (50342, N'game',
            N'usp_Character_ApplyTribeScrollConversion: client-supplied target tribe is outside the playable 0..2 range (hardening of the legacy inert range guard).');

IF NOT EXISTS (SELECT 1
               FROM admin.ErrorCatalog
               WHERE ErrorNumber = 50343)
    INSERT INTO admin.ErrorCatalog (ErrorNumber, SchemaName, Description)
    VALUES (50343, N'game', N'usp_Character_ApplyTribeScrollConversion: unknown CharacterId.');

IF NOT EXISTS (SELECT 1
               FROM admin.ErrorCatalog
               WHERE ErrorNumber = 50344)
    INSERT INTO admin.ErrorCatalog (ErrorNumber, SchemaName, Description)
    VALUES (50344, N'game',
            N'usp_Character_ApplyTribeScrollConversion: target tribe already matches this character''s previous tribe (unconditional -- no neutral exemption, unlike the book path).');

IF NOT EXISTS (SELECT 1
               FROM admin.ErrorCatalog
               WHERE ErrorNumber = 50345)
    INSERT INTO admin.ErrorCatalog (ErrorNumber, SchemaName, Description)
    VALUES (50345, N'game',
            N'usp_Character_ApplyTribeScrollConversion: first-level is below the required LV_M33 (145 / max level).');

IF NOT EXISTS (SELECT 1
               FROM admin.ErrorCatalog
               WHERE ErrorNumber = 50346)
    INSERT INTO admin.ErrorCatalog (ErrorNumber, SchemaName, Description)
    VALUES (50346, N'game',
            N'usp_Character_ApplyTribeScrollConversion: character holds a tribe office (master, sub-master, or elected council seat) in its current tribe.');

IF NOT EXISTS (SELECT 1
               FROM admin.ErrorCatalog
               WHERE ErrorNumber = 50347)
    INSERT INTO admin.ErrorCatalog (ErrorNumber, SchemaName, Description)
    VALUES (50347, N'game', N'usp_Character_ApplyTribeScrollConversion: character belongs to a guild.');

IF NOT EXISTS (SELECT 1
               FROM admin.ErrorCatalog
               WHERE ErrorNumber = 50348)
    INSERT INTO admin.ErrorCatalog (ErrorNumber, SchemaName, Description)
    VALUES (50348, N'game', N'usp_Character_ApplyTribeScrollConversion: character has one or more registered friends.');
