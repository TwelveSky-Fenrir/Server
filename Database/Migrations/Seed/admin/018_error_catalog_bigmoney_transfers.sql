-- New file rather than an edit to an earlier error-catalog script: applied scripts are never edited.
-- Backs the three new BigMoney ("1B") transfer/conversion procedures added by this pass
-- (usp_Character_AdjustBigMoneyStore, usp_AccountVault_TransferBigMoneyWithCharacter,
-- usp_Character_AdjustBigMoneyConversion) -- CZ_PROCESS_DATA_SEND tSort 241/242/244/245/246/247, the last
-- pieces of the C8-bank-store container-move contract not already covered by the existing Store/Bank item
-- and regular-money procedures (usp_CharacterItems_Replace(Two)Container(s), usp_Character_AdjustStoreMoney,
-- usp_AccountVault_Transfer(Item/Money)WithCharacter). One distinct code per procedure/guard, matching this
-- schema's own "never collapse two distinguishable failure causes into one shared number" convention;
-- usp_AccountVault_TransferBigMoneyWithCharacter gets two (Characters-side guard, then AccountVault-side
-- guard) mirroring usp_AccountVault_TransferMoneyWithCharacter's own 50338/50339 pair.
IF NOT EXISTS (SELECT 1 FROM admin.ErrorCatalog WHERE ErrorNumber = 50349)
    INSERT INTO admin.ErrorCatalog (ErrorNumber, SchemaName, Description)
    VALUES (50349, N'game',
            N'usp_Character_AdjustBigMoneyStore: unknown character or insufficient/over-cap BigMoney/BigStoreMoney (999-unit MAX_NUMBER_SIZE2 cap).');

IF NOT EXISTS (SELECT 1 FROM admin.ErrorCatalog WHERE ErrorNumber = 50350)
    INSERT INTO admin.ErrorCatalog (ErrorNumber, SchemaName, Description)
    VALUES (50350, N'game',
            N'usp_AccountVault_TransferBigMoneyWithCharacter: unknown character or insufficient/over-cap inventory BigMoney.');

IF NOT EXISTS (SELECT 1 FROM admin.ErrorCatalog WHERE ErrorNumber = 50351)
    INSERT INTO admin.ErrorCatalog (ErrorNumber, SchemaName, Description)
    VALUES (50351, N'game',
            N'usp_AccountVault_TransferBigMoneyWithCharacter: insufficient or over-cap account vault BigMoney (Money2).');

IF NOT EXISTS (SELECT 1 FROM admin.ErrorCatalog WHERE ErrorNumber = 50352)
    INSERT INTO admin.ErrorCatalog (ErrorNumber, SchemaName, Description)
    VALUES (50352, N'game',
            N'usp_Character_AdjustBigMoneyConversion: unknown character or insufficient/over-cap balance for this Money/BigMoney conversion.');
