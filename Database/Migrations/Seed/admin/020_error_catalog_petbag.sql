IF NOT EXISTS (SELECT 1
               FROM admin.ErrorCatalog
               WHERE ErrorNumber = 50350)
    INSERT INTO admin.ErrorCatalog (ErrorNumber, SchemaName, Description)
    VALUES (50350, 'game',
            N'usp_CharacterPetBag_Deposit / usp_CharacterPetBag_Rearrange: pet-bag destination slot already occupied.');

IF NOT EXISTS (SELECT 1
               FROM admin.ErrorCatalog
               WHERE ErrorNumber = 50351)
    INSERT INTO admin.ErrorCatalog (ErrorNumber, SchemaName, Description)
    VALUES (50351, 'game',
            N'usp_CharacterPetBag_Rearrange / usp_CharacterPetBag_Withdraw: pet-bag source slot is empty.');
