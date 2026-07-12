-- New file rather than an edit to an earlier error-catalog script: applied scripts are never edited.
-- Registers the three BigMoney ("1B" unit) error codes that survived a Wave-7 duplicate-implementation
-- collision between two independent BigMoney passes (Characters-namespace vs. Inventory-namespace
-- IBigMoneyRepository; see Infrastructure/Fenrir.Data/ServiceCollectionExtensions.cs's own comment at the
-- IBigMoneyRepository registration for the full writeup). The losing implementation's procedure/codes
-- (old usp_AccountVault_TransferBigMoneyWithCharacter, codes 50350/50351) were superseded in place by a
-- direct in-place edit to StoredProcedures/game/usp_AccountVault_TransferBigMoneyWithCharacter.sql (same
-- procedure name, new codes 50353/50354; there is no "Migrations/046" script anywhere in this repo's
-- history, that citation was always a phantom) -- 50350/50351 are NOT orphaned catalog gaps from this
-- collision, they were reassigned to the unrelated pet-bag procedures (Migrations/044_character_petbag_procs.sql)
-- and are tracked separately (see this file's own note below).
-- 50349 (game.usp_Character_AdjustBigStoreMoney, StoredProcedures/game/, tSort 241/244 Inventory<->Store)
-- was never collided and simply had no catalog row yet.
IF NOT EXISTS (SELECT 1
               FROM admin.ErrorCatalog
               WHERE ErrorNumber = 50349)
    INSERT INTO admin.ErrorCatalog (ErrorNumber, SchemaName, Description)
    VALUES (50349, 'game',
            N'usp_Character_AdjustBigStoreMoney: unknown character or insufficient/over-cap balance for this BigMoney/BigStoreMoney adjustment (999-unit cap, tSort 241/244).');

IF NOT EXISTS (SELECT 1
               FROM admin.ErrorCatalog
               WHERE ErrorNumber = 50353)
    INSERT INTO admin.ErrorCatalog (ErrorNumber, SchemaName, Description)
    VALUES (50353, 'game',
            N'usp_AccountVault_TransferBigMoneyWithCharacter: unknown character or insufficient/over-cap BigMoney balance on the character side of this vault transfer (999-unit cap, tSort 242/245).');

IF NOT EXISTS (SELECT 1
               FROM admin.ErrorCatalog
               WHERE ErrorNumber = 50354)
    INSERT INTO admin.ErrorCatalog (ErrorNumber, SchemaName, Description)
    VALUES (50354, 'game',
            N'usp_AccountVault_TransferBigMoneyWithCharacter: insufficient/over-cap account vault BigMoney balance for this transfer (999-unit cap, tSort 242/245).');

-- NOTE (not part of this file's own registration, flagged for a follow-up seed script): 50350/50351
-- (Migrations/044_character_petbag_procs.sql, "Pet-bag destination slot already occupied" /
-- "Pet-bag source slot is empty") were found to ALSO have no admin.ErrorCatalog row during this session's
-- audit -- out of scope for the specific 3-code gap this file was asked to close, but a real gap. See
-- .claude/agent-memory/fenrir-database-engineer/errorcatalog_seed_gap_wave8.md.
