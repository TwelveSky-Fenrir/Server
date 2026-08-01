-- New file rather than an edit to an earlier error-catalog script: applied scripts are never edited.
-- Registers 50361 for the ContainerA=ContainerB guard added by
-- Migrations/044_characteritems_bag_position.sql's usp_CharacterItems_ReplaceTwoContainersV2 -- kept
-- distinct from 50260 (the same guard on V1) per admin.ErrorCatalog's own collision rule: a number may be
-- shared only when every thrower is the same procedure family raising the identical failure, and V1/V2 are
-- two different procedures.
IF NOT EXISTS (SELECT 1
               FROM admin.ErrorCatalog
               WHERE ErrorNumber = 50361)
    INSERT INTO admin.ErrorCatalog (ErrorNumber, SchemaName, Description)
    VALUES (50361,
            'game',
            N'usp_CharacterItems_ReplaceTwoContainersV2: ContainerA and ContainerB must differ -- use usp_CharacterItems_ReplaceContainerV2 for a same-container move.');
