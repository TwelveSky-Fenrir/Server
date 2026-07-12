-- New file rather than an edit to an earlier error-catalog script: applied scripts are never edited.
-- Backs game.usp_Character_ApplyTribeConversion (tribe conversion via Book of Noble Dragon/Royal
-- Serpent/Grand Tiger V2, world.Items 99014/99015/99016) -- one distinct code per precondition/abort kind,
-- matching the behavior contract's own 8-way precondition list.
--
-- Renamed from 010_error_catalog_tribe_conversion.sql (2026-07-12, manifest-structure-cleanliness): the
-- old "010" leading number collided with the unrelated 010_error_catalog_death_protection.sql. Both files
-- were always applied correctly in a stable relative order registering non-overlapping codes, so this was
-- a pure numbering-convention fix, not a functional one -- see _manifest.txt's own comment on this entry.
IF NOT EXISTS (SELECT 1
               FROM admin.ErrorCatalog
               WHERE ErrorNumber = 50313)
    INSERT INTO admin.ErrorCatalog (ErrorNumber, SchemaName, Description)
    VALUES (50313, 'game',
            N'usp_Character_ApplyTribeConversion: ItemId is not one of the three tribe-conversion books (99014/99015/99016).');

IF NOT EXISTS (SELECT 1
               FROM admin.ErrorCatalog
               WHERE ErrorNumber = 50314)
    INSERT INTO admin.ErrorCatalog (ErrorNumber, SchemaName, Description)
    VALUES (50314, 'game', N'usp_Character_ApplyTribeConversion: unknown CharacterId.');

IF NOT EXISTS (SELECT 1
               FROM admin.ErrorCatalog
               WHERE ErrorNumber = 50315)
    INSERT INTO admin.ErrorCatalog (ErrorNumber, SchemaName, Description)
    VALUES (50315, 'game',
            N'usp_Character_ApplyTribeConversion: target tribe already matches this character''s previous tribe (current tribe is not the neutral/"nangin" code 3).');

IF NOT EXISTS (SELECT 1
               FROM admin.ErrorCatalog
               WHERE ErrorNumber = 50316)
    INSERT INTO admin.ErrorCatalog (ErrorNumber, SchemaName, Description)
    VALUES (50316, 'game',
            N'usp_Character_ApplyTribeConversion: combined Level+Level2 does not meet the consumed item''s LevelLimit+MartialLevelLimit.');

IF NOT EXISTS (SELECT 1
               FROM admin.ErrorCatalog
               WHERE ErrorNumber = 50317)
    INSERT INTO admin.ErrorCatalog (ErrorNumber, SchemaName, Description)
    VALUES (50317, 'game',
            N'usp_Character_ApplyTribeConversion: character holds a tribe office (master, sub-master, or elected council seat) in its current tribe.');

IF NOT EXISTS (SELECT 1
               FROM admin.ErrorCatalog
               WHERE ErrorNumber = 50318)
    INSERT INTO admin.ErrorCatalog (ErrorNumber, SchemaName, Description)
    VALUES (50318, 'game', N'usp_Character_ApplyTribeConversion: character belongs to a guild.');

IF NOT EXISTS (SELECT 1
               FROM admin.ErrorCatalog
               WHERE ErrorNumber = 50319)
    INSERT INTO admin.ErrorCatalog (ErrorNumber, SchemaName, Description)
    VALUES (50319, 'game', N'usp_Character_ApplyTribeConversion: character has one or more registered friends.');

IF NOT EXISTS (SELECT 1
               FROM admin.ErrorCatalog
               WHERE ErrorNumber = 50320)
    INSERT INTO admin.ErrorCatalog (ErrorNumber, SchemaName, Description)
    VALUES (50320, 'game',
            N'usp_Character_ApplyTribeConversion: an equipped item has no target-tribe equivalent in world.TribeItemEquivalences -- whole conversion aborted, no state changed.');
