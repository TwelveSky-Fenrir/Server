IF NOT EXISTS (SELECT 1
               FROM admin.ErrorCatalog
               WHERE ErrorNumber = 50337)
    INSERT INTO admin.ErrorCatalog (ErrorNumber, SchemaName, Description)
    VALUES (50337, 'game',
            N'usp_Character_AdjustStoreMoney: unknown character or insufficient balance for this Money/StoreMoney adjustment.');

IF NOT EXISTS (SELECT 1
               FROM admin.ErrorCatalog
               WHERE ErrorNumber = 50338)
    INSERT INTO admin.ErrorCatalog (ErrorNumber, SchemaName, Description)
    VALUES (50338, 'game',
            N'usp_AccountVault_TransferMoneyWithCharacter: unknown character or insufficient wallet balance for this vault transfer.');

IF NOT EXISTS (SELECT 1
               FROM admin.ErrorCatalog
               WHERE ErrorNumber = 50339)
    INSERT INTO admin.ErrorCatalog (ErrorNumber, SchemaName, Description)
    VALUES (50339, 'game',
            N'usp_AccountVault_TransferMoneyWithCharacter: insufficient account vault balance for this transfer.');
