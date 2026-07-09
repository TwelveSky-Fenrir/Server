-- Item static-data load-time validation gap (fenrir-database-engineer behavior contract "Item static-data
-- load-time validation (world.Items / legacy ITEM_INFO)"): closes the storage-level pieces the contract's
-- Edge cases section calls out as concrete gaps -- a column too narrow to even represent a legacy-valid
-- value, and a cross-field bound with no per-column analogue at all. Not an in-place edit of the
-- already-applied Tables/world/Items.sql (DbMigrator journals every applied script by SHA-256 and refuses
-- a changed re-apply -- see Database/_manifest.txt's own header and the
-- sql-server-2025-stored-procedure-conventions skill), so every change below ships as its own migration.
--
-- Numbered 038 (not 032) because 032-036 were concurrently claimed by the Skill/Npc/Monster/GemSocket
-- static-data range-check migrations landing in the same batch of work -- see Database/_manifest.txt for
-- the full ordering.
--
-- 1) Column-width fixes (Server/Header/S15_MyShare.cpp:880-888 -- MAX_ITEM_STATUS_ATTRIBUTE = 10000; the
--    same file's Item_CheckValidElement body, cited per-field below):
--      * CheckDateItem: legacy valid range is 0-365 (Server/Header/S15_MyShare.cpp:1065-1069), not the
--        "0-30" this table's own inline comment claims (a separate, smaller transcription-accuracy issue
--        the contract flags on top of the width problem -- left uncorrected in Tables/world/Items.sql
--        itself, since that script is already applied; this header is the correction of record instead).
--        TINYINT tops out at 255, so 256-365 -- legacy-valid values -- could never even be stored.
--      * DataNumber3D / AddDataNumber3D: legacy valid range for both is 0-10000
--        (Server/Header/S15_MyShare.cpp:950-959), also stored as TINYINT (max 255). AddDataNumber3D is
--        commented "always 0 in current data" and so carries low practical risk today, but DataNumber3D
--        has no such comment and the column type alone cannot represent the full legacy-valid range for
--        either field -- both widened together for consistency and structural fidelity.
--    All three widen TINYINT -> SMALLINT (0-10000/0-365 both fit comfortably; SMALLINT matches the type
--    already used for every other 0-10000-range stat column on this table, e.g. Strength/Dexterity/
--    Vitality/AttackBlock). NOT NULL is restated explicitly: SQL Server's ALTER COLUMN drops an existing
--    NOT NULL back to nullable unless the new definition repeats it.
--
-- 2) New CHECK constraints on the 3 widened columns, mirroring Migrations/030_levels_rangeinfo3_check_
--    constraint.sql's own reasoning: once a column is wider than the legacy bound it exists to represent
--    (SMALLINT permits -32768..32767; the legacy rules above are 0-365 and 0-10000), the column type alone
--    no longer enforces that bound, so a CHECK constraint closes the gap the widening itself just opened.
--
-- 3) A new compound (multi-column, same-row) CHECK constraint reproducing PotionType2's conditional bound:
--    valid range is normally 0-10000, but narrows to 1-3 whenever PotionType1 equals 9
--    (Server/Header/S15_MyShare.cpp:1130-1147). The contract's Edge cases section calls this out explicitly
--    as having "no per-column analogue at all in the current schema" -- a plain single-column CHECK on
--    PotionType2 cannot express a bound conditional on a sibling column's value, but a CHECK constraint may
--    reference more than one column of the same row, which is exactly what this does. PotionType1's own
--    0-16 bound is deliberately NOT added here (it is an ordinary per-column legacy bound like Type 1-8 or
--    Sort 1-99, none of which this script touches -- those stay boot-time-loader validation, matching how
--    030/031 left everything not individually flagged as a storage-level gap to their own contracts'
--    corresponding C# loader checks).
--
-- Deliberately NOT added here (same reasoning as 031's own documented QuestId omission): a CHECK
-- (ItemId BETWEEN 1 AND 99999). ItemId is boot-time, generator-assigned reference data (already
-- PRIMARY KEY / NOT NULL), not externally supplied input, and the contract's own Inputs section notes the
-- other half of the legacy ItemId rule -- "must equal its physical position in the on-disk array plus
-- one" -- has no relational-table analogue at all, since ItemId is this table's primary key rather than an
-- array slot.
--
-- Existing seed data (Migrations/Seed/world/080_items.sql, 34,353 rows) was checked against every bound
-- below before adding these as validating (WITH CHECK, the ALTER TABLE default) constraints: CheckDateItem
-- ranges 0-30, DataNumber3D ranges 0-56, AddDataNumber3D is always 0, and the 3 rows with PotionType1 = 9
-- all have PotionType2 in {1,2,3} -- every bound here is comfortably satisfied by the shipped dataset, so
-- this migration cannot fail against it.
ALTER TABLE world.Items
    ALTER COLUMN CheckDateItem SMALLINT NOT NULL;
GO

ALTER TABLE world.Items
    ALTER COLUMN DataNumber3D SMALLINT NOT NULL;
GO

ALTER TABLE world.Items
    ALTER COLUMN AddDataNumber3D SMALLINT NOT NULL;
GO

ALTER TABLE world.Items
    ADD CONSTRAINT CK_Items_CheckDateItem CHECK (CheckDateItem BETWEEN 0 AND 365);
GO

ALTER TABLE world.Items
    ADD CONSTRAINT CK_Items_DataNumber3D CHECK (DataNumber3D BETWEEN 0 AND 10000);
GO

ALTER TABLE world.Items
    ADD CONSTRAINT CK_Items_AddDataNumber3D CHECK (AddDataNumber3D BETWEEN 0 AND 10000);
GO

ALTER TABLE world.Items
    ADD CONSTRAINT CK_Items_PotionType2Range CHECK (
        (PotionType1 = 9 AND PotionType2 BETWEEN 1 AND 3)
            OR (PotionType1 <> 9 AND PotionType2 BETWEEN 0 AND 10000)
        );
GO
