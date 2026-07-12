-- New file rather than an edit to an earlier error-catalog script: applied scripts are never edited.
-- Registers 50337-50339 for the three new stored procedures backing the previously-unwired Store/coffre and
-- Save/vault (account bank) money-transfer CZ_PROCESS_DATA_SEND sub-actions (tSort 226/227/231/232):
-- usp_Character_AdjustStoreMoney, usp_AccountVault_TransferMoneyWithCharacter.
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
