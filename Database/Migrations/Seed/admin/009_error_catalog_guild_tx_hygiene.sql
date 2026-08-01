-- New file rather than an edit to an earlier error-catalog script: applied scripts are never edited.
-- Backs usp_Guild_CreateAndDebitMoney/usp_Guild_UpgradeAndDebitMoney (guild-tx-hygiene fix, not legacy parity).
IF
    NOT EXISTS (SELECT 1
                FROM admin.ErrorCatalog
                WHERE ErrorNumber = 50277)
    INSERT INTO admin.ErrorCatalog (ErrorNumber, SchemaName, Description)
    VALUES (50277, 'game',
            N'usp_Guild_CreateAndDebitMoney: unknown character or insufficient money balance for the guild creation cost.');

IF
    NOT EXISTS (SELECT 1
                FROM admin.ErrorCatalog
                WHERE ErrorNumber = 50278)
    INSERT INTO admin.ErrorCatalog (ErrorNumber, SchemaName, Description)
    VALUES (50278, 'game',
            N'usp_Guild_UpgradeAndDebitMoney: unknown character or insufficient money balance for the guild upgrade cost.');
