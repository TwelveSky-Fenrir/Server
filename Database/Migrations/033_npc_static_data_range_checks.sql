-- Npc static-data load-time field validation (legacy MyShm::Load_Npc / Npc_CheckValidElement audit) --
-- fenrir-database-engineer behavior contract "Npc static-data loading and validation (NPC_INFO)". Closes
-- the contract's core finding: "Range constraints on Tribe, Type, DataSortNumber2D/3D, Size1-3,
-- NpcMenuOptions.OptionId, and NpcGambleCosts.Value are enforced by the legacy loader but have no
-- equivalent enforcement anywhere in the C# schema or import path" -- confirmed by reading all six
-- Database/Tables/world/Npc*.sql files, Database/_manifest.txt (no later migration ever added one), and
-- Tools/Fenrir.Tools.LegacyDataImport/Legacy/Validation/NpcValidation.cs (an ad-hoc diagnostic print tool,
-- not a validation gate). Tables/world/Npcs.sql, Tables/world/NpcMenuOptions.sql and
-- Tables/world/NpcGambleCosts.sql are already applied/journaled, so this ships as a new corrective
-- migration rather than an in-place edit of any of them -- same pattern as
-- Migrations/030_levels_rangeinfo3_check_constraint.sql, Migrations/031_quest_step_and_reward_amount_range_checks.sql
-- and Migrations/032_skill_static_data_range_checks.sql.
--
-- Legacy never lets an out-of-range NPC record reach the shared-memory NPC_INFO table at all:
-- Npc_CheckValidElement (Server/Header/S15_MyShare.cpp:1836-1963) rejects the whole 500-slot Load_Npc call
-- on the first offending record (Server/Header/S15_MyShare.cpp:648-655), which is fatal to both
-- ts25sharemem's boot sequence (Server/ts25sharemem/main.cpp:80-86) and ts25zone's fixed startup data-load
-- sequence (Server/ts25zone/GameSystem/GameSystem_05_Npc.cpp:8-15,
-- Server/ts25zone/GameSystem/GameSystem_00_Load.cpp:42-46, Server/ts25zone/S01_MainApplication.cpp:274-281).
-- A relational CHECK constraint is the closest available Fenrir equivalent of "this value can never be
-- stored" -- stricter than legacy in one respect (enforced on every write, not just at one boot-time pass)
-- but matching legacy's intent that these fields are bounded.
--
-- Bounds, all per Server/Header/S15_MyShare.cpp and Server/Header/Protocol/STRUCT.h:206-235:
--   Tribe 1-5 (:1887-1890), Type 1-17 (:1891-1894), DataSortNumber2D/3D 1-10000 each (:1895-1902),
--   Size1/2/3 1-1000 each (:1903-1909), NpcMenuOptions.OptionId exactly 1 or 2, 0 explicitly invalid
--   (:1910-1916), NpcGambleCosts.Value 0-100,000,000 (:1953-1962).
--
-- Existing seed data (Migrations/Seed/world/Npcs.sql, which seeds all six world.Npc* tables together)
-- checked against every bound below before adding these as validating (WITH CHECK, the ALTER TABLE
-- default) constraints: across 131 seeded NPC rows, Tribe ranges 1-5, Type ranges 1-17, DataSortNumber2D
-- ranges 1948-3209, DataSortNumber3D ranges 1-66, Size1/2/3 range 5-27; across 13,100 seeded
-- NpcMenuOptions rows, OptionId is only ever 1 or 2; across 13,050 seeded NpcGambleCosts rows, Value ranges
-- 500-683,746 -- all comfortably inside the new bounds, so this migration cannot fail against the shipped
-- dataset.
--
-- Deliberately NOT added here, per the contract's own scoping:
--   * world.Npcs.Name's length-prefixed NVARCHAR(28) vs. legacy's in-band-null-terminator 28-byte buffer
--     (effectively 27 usable characters) -- the contract explicitly ranks this "low practical risk" and
--     "not observed whether any current seed row is at that boundary" rather than listing it alongside the
--     numeric range constraints it calls out as "the core finding"; current seed data's longest name is 23
--     characters, nowhere near either ceiling. Left as a documented open item, not silently dropped.
--   * A CHECK (NpcId BETWEEN 1 AND 500) plus the "index must equal its own array slot position" invariant --
--     the contract's Edge cases section explicitly leaves this unresolved ("not observed whether any of the
--     500 on-disk slots actually exercises this branch ... flag for cpp-ts25-explorer re-check if this
--     matters downstream") rather than confirming it as a concrete gap. NpcId is also already
--     PRIMARY KEY / NOT NULL on world.Npcs and currently populated 1-424, so no live data would be affected
--     either way; same reasoning Migrations/031 used to exclude the analogous QuestId bound.
--   * world.NpcShopItems.ItemId / world.NpcSkillOffers.SkillId -- the contract's own Edge cases and
--     Citations sections confirm these are already FK-constrained (FK_NpcShopItems_Items,
--     FK_NpcSkillOffers_Skills), which is stricter than legacy's raw 0-99999 / 0-300 numeric bound, not a
--     gap to close.
--   * world.Npcs.SpeechNum (legacy nSpeechNum) -- the contract confirms this legacy field is parsed by the
--     import tool and then discarded, with no equivalent column anywhere in the C# schema, but also confirms
--     no legacy runtime code in ts25zone ever reads it back after load-time validation. Reintroducing a
--     column purely to re-validate a value nothing downstream consumes is out of scope for this migration;
--     flagged in the contract as an accepted, currently-inert gap rather than one requiring storage-level
--     enforcement.
ALTER TABLE world.Npcs
    ADD CONSTRAINT CK_Npcs_Tribe CHECK (Tribe BETWEEN 1 AND 5);
GO

ALTER TABLE world.Npcs
    ADD CONSTRAINT CK_Npcs_Type CHECK (Type BETWEEN 1 AND 17);
GO

ALTER TABLE world.Npcs
    ADD CONSTRAINT CK_Npcs_DataSortNumber2D CHECK (DataSortNumber2D BETWEEN 1 AND 10000);
GO

ALTER TABLE world.Npcs
    ADD CONSTRAINT CK_Npcs_DataSortNumber3D CHECK (DataSortNumber3D BETWEEN 1 AND 10000);
GO

ALTER TABLE world.Npcs
    ADD CONSTRAINT CK_Npcs_Size1 CHECK (Size1 BETWEEN 1 AND 1000);
GO

ALTER TABLE world.Npcs
    ADD CONSTRAINT CK_Npcs_Size2 CHECK (Size2 BETWEEN 1 AND 1000);
GO

ALTER TABLE world.Npcs
    ADD CONSTRAINT CK_Npcs_Size3 CHECK (Size3 BETWEEN 1 AND 1000);
GO

ALTER TABLE world.NpcMenuOptions
    ADD CONSTRAINT CK_NpcMenuOptions_OptionId CHECK (OptionId IN (1, 2));
GO

ALTER TABLE world.NpcGambleCosts
    ADD CONSTRAINT CK_NpcGambleCosts_Value CHECK (Value BETWEEN 0 AND 100000000);
GO
