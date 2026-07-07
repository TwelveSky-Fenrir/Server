-- Documentation-only corrective fix-forward (creation-persistence-full-reaudit finding, severity Minor):
-- Database/Tables/game/Characters.sql's own DDL inline comment on the Experience column (already applied/
-- journaled -- never edited in place, per this repo's SHA-256 replay-safety rule; see
-- Migrations/028_starter_kit_equipment_doc_correction.sql for the identical precedent this migration follows)
-- reads "aExp1+aExp2 combined". That characterization is unsupported by either cited legacy source and is
-- corrected here:
--
--   * Server/BuildEU33/DB/nxtserver.sql:41-43 declares aLevel2, aExp1, and aExp2 as three fully separate
--     int(11) columns, each with its own independent DEFAULT 0 -- no combined/derived column exists at this
--     location, and nothing sums the two.
--   * Server/ts25zone/UpperCom/S06_MyUpperCom05.cpp:229-240 (specifically :234-236) persists aExp1 and aExp2
--     as two independent values, each carrying its own update timestamp, in one statement -- again never
--     summed or otherwise merged.
--   * Fenrir's own creation path already corroborates the "aExp1, not a sum" reading: Server/ts25login/
--     S04_MyWork02.cpp:1105 grants aExp1 = MAX_NUMBER_SIZE (2,000,000,000, Server/Header/Protocol/DEFINE.h:365)
--     unconditionally at creation, and usp_Character_CreateWithStarterKit persists that exact literal into
--     this column (Migrations/018_character_previous_tribe_and_mount_readpath.sql:113), not some pre-summed
--     total. usp_Character_PersistProgressBatch/usp_Character_GetForWorldEntry likewise always read/write
--     Experience and Exp2 as two independent values -- no procedure in this schema sums them either.
--
-- This also resolves the structural contradiction the finding raised between this column and
-- Database/Tables/game/Characters.sql:24's Exp2 column (already independently documented as mirroring aExp2):
-- a second column also claiming to fold aExp2 into a sum would double-count that same legacy field. With the
-- "combined" claim removed, both columns are now independently and consistently described.
--
-- What this migration deliberately does NOT resolve (per the creation-persistence-full-reaudit behavior
-- contract's own "edge cases" section): the actual runtime split in meaning between aExp1 and aExp2 -- e.g.
-- whether aExp1 only advances pre-rebirth-cap while aExp2 advances only post-rebirth, or some other division
-- -- is not established by any citation available to this pass. Flagged for a cpp-zone-gameplay-analyst /
-- cpp-ts25-explorer re-check before any further schema-split decision; this migration only corrects the
-- factually-wrong comment, it does not split or reinterpret the column.
--
-- Same EXECUTE/@parameter-must-be-constant-or-variable and sql_variant/NVARCHAR(MAX)-incompatibility
-- constraints as 028 apply here (Microsoft Learn's sp_addextendedproperty reference and a real SQL Server
-- 2025 instance both confirm both restrictions) -- the description is built into a local NVARCHAR(4000)
-- variable first, then passed as that variable.
DECLARE @ExperienceDescription NVARCHAR(4000) =
    N'Persisted analogue of legacy aExp1 -- NOT "aExp1+aExp2 combined" (this column''s original CREATE TABLE '
        + N'comment, corrected here; see Migrations/031_character_experience_column_doc_correction.sql for the '
        + N'full citation trail, never edited in place per this repo''s SHA-256 replay-safety rule). '
        + N'Server/BuildEU33/DB/nxtserver.sql:41-43 declares aExp1/aExp2 as two fully separate int(11) columns, '
        + N'each with its own independent default; Server/ts25zone/UpperCom/S06_MyUpperCom05.cpp:229-240 persists '
        + N'them as two independent values (each with its own update timestamp) in one statement, never summed. '
        + N'Server/ts25login/S04_MyWork02.cpp:1105 grants aExp1 = MAX_NUMBER_SIZE (2,000,000,000, Server/Header/'
        + N'Protocol/DEFINE.h:365) at creation, and usp_Character_CreateWithStarterKit persists that exact literal '
        + N'into this column (Migrations/018_character_previous_tribe_and_mount_readpath.sql:113), not a pre-summed '
        + N'total. This column and Exp2 (this table''s own separate column, independently mirroring aExp2) are '
        + N'never summed anywhere in Fenrir either -- usp_Character_PersistProgressBatch/usp_Character_'
        + N'GetForWorldEntry both read/write them as two independent values. The exact runtime split in meaning '
        + N'between aExp1 and aExp2 (e.g. whether aExp1 only advances pre-rebirth-cap while aExp2 advances only '
        + N'post-rebirth, or some other division) is NOT established by any citation available to this pass -- '
        + N'flagged for a cpp-zone-gameplay-analyst/cpp-ts25-explorer re-check before any further schema-split '
        + N'decision, not resolved by this correction.';

EXECUTE sys.sp_addextendedproperty
        @name = N'MS_Description',
        @value = @ExperienceDescription,
        @level0type = N'SCHEMA', @level0name = N'game',
        @level1type = N'TABLE', @level1name = N'Characters',
        @level2type = N'COLUMN', @level2name = N'Experience';
GO
