IF NOT EXISTS (SELECT 1
               FROM admin.ErrorCatalog
               WHERE ErrorNumber = 50361)
    INSERT INTO admin.ErrorCatalog (ErrorNumber, SchemaName, Description)
    VALUES (50361,
            'game',
            N'usp_CharacterItems_ReplaceTwoContainersV2: ContainerA and ContainerB must differ -- use usp_CharacterItems_ReplaceContainerV2 for a same-container move.');
