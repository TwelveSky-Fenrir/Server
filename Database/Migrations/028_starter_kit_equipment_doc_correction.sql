-- Documentation-only corrective fix-forward: Database/Tables/world/StarterKitEquipment.sql's own DDL header
-- comment (already applied/journaled -- never edited in place, per this repo's SHA-256 replay-safety rule)
-- has been stale/misleading since 015_starter_kit_elite_grant.sql/086_starter_kit_equipment_elite_correction.sql
-- landed:
--
--   * It claims the table reflects "the non-USE_CUSTOME_CREATE branch" of
--     Server/ts25login/S04_MyWork02.cpp's PreviousTribe switch. That was true only for
--     082_starter_kit_equipment.sql's original seed rows. 015_starter_kit_elite_grant.sql's own header comment
--     (see its full citation there) established that USE_CUSTOME_CREATE is force-defined unconditionally at
--     S04_MyWork02.cpp:1, with no matching #undef anywhere under Server/ and no ExcludedFromBuild condition in
--     ts25login.vcxproj -- so the USE_CUSTOME_CREATE ("G12 Elite Normal Set") branch is what ships in every
--     build configuration, and 086_starter_kit_equipment_elite_correction.sql's header comment already records
--     that it replaced 082's dead-branch rows for exactly this reason
--     (DELETE FROM world.StarterKitEquipment; followed by a fresh INSERT). The table's actual, current seed
--     data is 086's rows, not 082's -- 082 ran first in Database/_manifest.txt order but every one of its rows
--     was deleted by 086 immediately after.
--   * It lists only EquipSlot 2/3/5/7 (Armor/Gloves/Boots/Weapon) as populated, omitting EquipSlot 0 (Amulet)
--     and EquipSlot 4 (Ring) -- both of which 086 seeds one row of per PreviousTribe, following the
--     FEQUIP_TYPE enum (Server/Header/Protocol/STRUCT.h:1662-1676: EAMULET=0, ERING=4) over
--     S04_MyWork02.cpp's own swapped inline comments (see 015's header for that citation). The table today
--     seeds exactly 8 rows per PreviousTribe (Amulet/Armor/Gloves/Ring/Boots, one row each, plus 3 Weapon
--     alternatives), 24 rows total -- not the 4-row-per-tribe/12-row-total shape the stale comment describes.
--   * It doesn't mention RawWeaponCode at all: 015_starter_kit_elite_grant.sql ALTER TABLE'd this TINYINT NULL
--     column on afterward (NULL for the 5 unconditional Amulet/Armor/Gloves/Ring/Boots grants, holding the raw
--     client-selectable code -- 5/6/7, 11/12/13, or 17/18/19 -- for each of the 3 Weapon rows per race; see
--     usp_StarterKit_GetByPreviousTribe/CreateAvatarService.TryResolveWeaponItemId for the read/match side).
--
-- Rather than editing Tables/world/StarterKitEquipment.sql's already-applied/journaled comment (which would
-- change its SHA-256 and make DbMigrator refuse to proceed against any environment that already applied it --
-- see Tools/Fenrir.Tools.DbMigrator/Program.cs's journal-hash-mismatch guard), this fix-forward attaches a
-- corrected, queryable MS_Description extended property to the table and to the RawWeaponCode column, the
-- idiomatic SQL Server way to document an object after its original CREATE without touching that DDL text.
--
-- EXECUTE's @parameter = value form only accepts a constant or a variable, never a general expression --
-- Microsoft Learn's "Specify parameters in a stored procedure" is explicit that a procedure call's parameter
-- values "must be constants or a variable" (a function call isn't even allowed, let alone a multi-term
-- expression). The multi-line N'...' + N'...' + ... concatenation below therefore can't be inlined directly
-- into sys.sp_addextendedproperty's @value = ... argument (SQL Server rejects it with "Incorrect syntax near
-- '+'.", confirmed against a real SQL Server 2025 instance) -- each description is built into a local
-- variable first, then passed as that variable.
--
-- The variable is NVARCHAR(4000), not (MAX): @value itself is sql_variant (Microsoft Learn's
-- sp_addextendedproperty reference: "@value is sql_variant... can't be more than 7,500 bytes"), and
-- sql_variant's own documented restrictions explicitly list nvarchar(max) as a type it can never hold
-- (confirmed against a real SQL Server 2025 instance: "Operand type clash: nvarchar(max) is incompatible
-- with sql_variant") -- unlike the '+' fix above, this is a hard type-system limit, not a spacing/style
-- choice. Both description strings below are ~1.1KB/~0.5KB of plain text, comfortably inside both the 4,000
-- nvarchar(4000)/8,000-byte cap and sp_addextendedproperty's own 7,500-byte @value cap.
DECLARE @TableDescription NVARCHAR(4000) =
    N'Server/ts25login/S04_MyWork02.cpp CREATE_AVATAR_SEND2, USE_CUSTOME_CREATE branch (force-defined '
        + N'unconditionally at S04_MyWork02.cpp:1, no #undef anywhere under Server/ -- this is the branch that '
        + N'ships in every build configuration; see Migrations/015_starter_kit_elite_grant.sql''s header '
        + N'comment for the full citation). Keyed by PreviousTribe (0=Noble Dragon,1=Royal Serpent,2=Grand '
        + N'Tiger), the client-chosen starting-kit template -- distinct from the playable Tribe (0-3) used for '
        + N'spawn/faction. EquipSlot follows FEQUIP_TYPE (Server/Header/Protocol/STRUCT.h:1662-1676): '
        + N'0=Amulet,2=Armor,3=Gloves,4=Ring,5=Boots,7=Weapon. Amulet/Armor/Gloves/Ring/Boots each own exactly '
        + N'one row per PreviousTribe; Weapon owns the 3 client-selectable alternatives (CreateAvatarRequest.'
        + N'Weapon, matched via RawWeaponCode, must match one of them) -- 8 rows per PreviousTribe, 24 total. '
        + N'Current seed data is Migrations/Seed/world/086_starter_kit_equipment_elite_correction.sql''s rows '
        + N'(which DELETEs and replaces 082_starter_kit_equipment.sql''s original, now-superseded '
        + N'dead-branch/no-Ring-or-Amulet/no-weapon-remap rows).';

EXECUTE sys.sp_addextendedproperty
        @name = N'MS_Description',
        @value = @TableDescription,
        @level0type = N'SCHEMA', @level0name = N'world',
        @level1type = N'TABLE', @level1name = N'StarterKitEquipment';
GO

DECLARE @RawWeaponCodeDescription NVARCHAR(4000) =
    N'Added by Migrations/015_starter_kit_elite_grant.sql (not present in the original CREATE TABLE '
        + N'script). NULL for the 5 unconditional per-race grants (Amulet/Armor/Gloves/Ring/Boots); holds the '
        + N'raw client-selectable weapon code (5/6/7, 11/12/13, or 17/18/19 depending on PreviousTribe) for '
        + N'each of the 3 Weapon (EquipSlot=7) rows per race. CreateAvatarService.TryResolveWeaponItemId '
        + N'matches CreateAvatarRequest.Weapon against this column, then grants that row''s ItemId (the elite '
        + N'weapon) -- never the raw code itself.';

EXECUTE sys.sp_addextendedproperty
        @name = N'MS_Description',
        @value = @RawWeaponCodeDescription,
        @level0type = N'SCHEMA', @level0name = N'world',
        @level1type = N'TABLE', @level1name = N'StarterKitEquipment',
        @level2type = N'COLUMN', @level2name = N'RawWeaponCode';
GO
