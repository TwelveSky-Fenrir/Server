-- Repo-wide ErrorCatalog completeness sweep (2026-07-12): a full multiline-aware grep of every
-- "THROW <number>" statement (some procedures split the number onto its own line, e.g.
-- usp_Character_AdjustMoney.sql / usp_GuildMember_Add.sql) across all 241 Database/StoredProcedures/**/*.sql
-- files, diffed against every ErrorNumber currently seeded anywhere under Migrations/Seed/admin/, found 11
-- codes that had been THROWn for a long time but never registered here. One of the eleven (50330/50331 --
-- two codes) was already flagged as a known gap in 010_error_catalog_death_protection.sql's own header
-- comment; the other nine (50100, 50201, 50202, 50210, 50211, 50212, 50221, 50310, 50311) are newly
-- discovered by this sweep. None of the eleven collide with an already-registered number, and no
-- already-registered number was found to be reused by two procedures for genuinely different
-- (non-analogous) failure meanings -- several codes ARE deliberately reused across multiple procedures for
-- the same failure kind (e.g. 50261 "adjustment would exceed the legacy money cap" is thrown by seven
-- different money-mutating procedures, 50235 "guild not found" by seven guild procedures), which is an
-- intentional, correct convention already used elsewhere in this catalog, not a collision. (2026-07-12
-- umbrella-description refresh also corrected 50230/50231/50233/50268/50269 to name a procedure added after
-- this sweep that reuses the same failure kind -- see 002_error_catalog_a3.sql / 003_error_catalog_social.sql's
-- own in-place edits.)
IF NOT EXISTS (SELECT 1
               FROM admin.ErrorCatalog
               WHERE ErrorNumber = 50100)
    INSERT INTO admin.ErrorCatalog (ErrorNumber, SchemaName, Description)
    VALUES (50100, 'game',
            N'usp_WorldState_EnsureInitialized: could not acquire the boot-serialization applock within the timeout.');

IF NOT EXISTS (SELECT 1
               FROM admin.ErrorCatalog
               WHERE ErrorNumber = 50201)
    INSERT INTO admin.ErrorCatalog (ErrorNumber, SchemaName, Description)
    VALUES (50201, 'game',
            N'usp_Character_Create/usp_Character_CreateWithStarterKit: character slot already occupied for this account.');

IF NOT EXISTS (SELECT 1
               FROM admin.ErrorCatalog
               WHERE ErrorNumber = 50202)
    INSERT INTO admin.ErrorCatalog (ErrorNumber, SchemaName, Description)
    VALUES (50202, 'game',
            N'usp_Character_Create/usp_Character_CreateWithStarterKit: character name already taken.');

IF NOT EXISTS (SELECT 1
               FROM admin.ErrorCatalog
               WHERE ErrorNumber = 50210)
    INSERT INTO admin.ErrorCatalog (ErrorNumber, SchemaName, Description)
    VALUES (50210, 'game', N'usp_TribeBank_Withdraw: insufficient tribe bank balance for this withdrawal.');

IF NOT EXISTS (SELECT 1
               FROM admin.ErrorCatalog
               WHERE ErrorNumber = 50211)
    INSERT INTO admin.ErrorCatalog (ErrorNumber, SchemaName, Description)
    VALUES (50211, 'game', N'usp_TribeBank_Deposit: tribe bank amount must be positive.');

IF NOT EXISTS (SELECT 1
               FROM admin.ErrorCatalog
               WHERE ErrorNumber = 50212)
    INSERT INTO admin.ErrorCatalog (ErrorNumber, SchemaName, Description)
    VALUES (50212, 'game', N'usp_TribeBank_DepositFromCharacter: character has no money to deposit into the tribe bank.');

IF NOT EXISTS (SELECT 1
               FROM admin.ErrorCatalog
               WHERE ErrorNumber = 50221)
    INSERT INTO admin.ErrorCatalog (ErrorNumber, SchemaName, Description)
    VALUES (50221, 'game', N'usp_AccountVault_AdjustMoney: insufficient account vault balance for this adjustment.');

IF NOT EXISTS (SELECT 1
               FROM admin.ErrorCatalog
               WHERE ErrorNumber = 50310)
    INSERT INTO admin.ErrorCatalog (ErrorNumber, SchemaName, Description)
    VALUES (50310, 'game', N'usp_TribeSubMaster_Set: tribe sub-master slot is already occupied.');

IF NOT EXISTS (SELECT 1
               FROM admin.ErrorCatalog
               WHERE ErrorNumber = 50311)
    INSERT INTO admin.ErrorCatalog (ErrorNumber, SchemaName, Description)
    VALUES (50311, 'game', N'usp_TribeSubMaster_Set: character already holds a sub-master slot in this tribe.');

-- 50330/50331 were already THROWn by usp_Tribe_SetMaster before this sweep and had already been flagged
-- (but deliberately left untouched, out of scope) by 010_error_catalog_death_protection.sql's own header
-- comment. Closing that pre-existing, previously-documented gap here.
IF NOT EXISTS (SELECT 1
               FROM admin.ErrorCatalog
               WHERE ErrorNumber = 50330)
    INSERT INTO admin.ErrorCatalog (ErrorNumber, SchemaName, Description)
    VALUES (50330, 'game', N'usp_Tribe_SetMaster: tribe not found.');

IF NOT EXISTS (SELECT 1
               FROM admin.ErrorCatalog
               WHERE ErrorNumber = 50331)
    INSERT INTO admin.ErrorCatalog (ErrorNumber, SchemaName, Description)
    VALUES (50331, 'game', N'usp_Tribe_SetMaster: character is not a member of this tribe.');
