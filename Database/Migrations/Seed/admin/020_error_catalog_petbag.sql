-- New file rather than an edit to an earlier error-catalog script: applied scripts are never edited.
-- Backs the pet-bag procedure family (Migrations/044_character_petbag_procs.sql:
-- usp_CharacterPetBag_Deposit/usp_CharacterPetBag_Rearrange/usp_CharacterPetBag_Withdraw, CZ_PROCESS_DATA_SEND
-- tSort 254/255/256) -- these two codes shipped with the procedures but were never registered in
-- admin.ErrorCatalog. Confirmed via grep sweep of Database/Migrations/Seed/admin/**: no prior row for either
-- number exists anywhere (50350/50351 previously described an unrelated BigMoney pair before a Wave-7
-- duplicate-implementation collision reassigned them here -- see
-- Migrations/Seed/admin/018_error_catalog_bigmoney_store_and_vault.sql's own trailing note and
-- .claude/agent-memory/fenrir-database-engineer/errorcatalog_seed_gap_wave8.md for the full history of this
-- gap being flagged-but-not-closed across two prior sessions).
IF NOT EXISTS (SELECT 1
               FROM admin.ErrorCatalog
               WHERE ErrorNumber = 50350)
    INSERT INTO admin.ErrorCatalog (ErrorNumber, SchemaName, Description)
    VALUES (50350, 'game', N'usp_CharacterPetBag_Deposit / usp_CharacterPetBag_Rearrange: pet-bag destination slot already occupied.');

IF NOT EXISTS (SELECT 1
               FROM admin.ErrorCatalog
               WHERE ErrorNumber = 50351)
    INSERT INTO admin.ErrorCatalog (ErrorNumber, SchemaName, Description)
    VALUES (50351, 'game', N'usp_CharacterPetBag_Rearrange / usp_CharacterPetBag_Withdraw: pet-bag source slot is empty.');
