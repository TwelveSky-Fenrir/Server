-- Gem socket static-data load-time field validation (legacy SOCKET_INFO / Load_GSocket /
-- GSocket_CheckValidElement audit) -- fenrir-database-engineer behavior contract "Gem socket static data
-- (SOCKET_INFO / world.GemSockets) load-time validation gate". Closes the contract's core finding: none of
-- the zero-bypass rules, the Type 1-46 range gate, or the five Type-band rules were enforced anywhere --
-- not in world.GemSockets' schema (Database/Tables/world/GemSockets.sql:1-13 lists only PK_GemSockets and
-- UQ_GemSockets_Type_Value02), not in either stored procedure (both are unfiltered/plain-predicate
-- SELECTs), not in the seed generation tool, and not in the runtime loader. world.GemSockets is already
-- applied/journaled, so this ships as a new corrective migration rather than an in-place edit -- same
-- pattern as Migrations/030_levels_rangeinfo3_check_constraint.sql,
-- Migrations/031_quest_step_and_reward_amount_range_checks.sql,
-- Migrations/032_skill_static_data_range_checks.sql and Migrations/033_npc_static_data_range_checks.sql.
--
-- Legacy never lets an out-of-range gem-socket record reach the shared-memory SOCKET_INFO table at all:
-- GSocket_CheckValidElement (Server/Header/S15_MyShare.cpp:2212-2268) rejects the whole 2891-slot
-- Load_GSocket call on the first offending record (Server/Header/S15_MyShare.cpp:749-756), which is fatal
-- to ts25zone's fixed startup data-load sequence (Server/ts25zone/GameSystem/GameSystem_08_Socket.cpp:8-15,
-- Server/ts25zone/GameSystem/GameSystem_00_Load.cpp:57-61, Server/ts25zone/S01_MainApplication.cpp:276-283,
-- 370-389). A relational CHECK constraint is the closest available Fenrir equivalent of "this value can
-- never be stored" -- stricter than legacy in one respect (enforced on every write, not just at one
-- boot-time pass) but matching legacy's intent that these fields are bounded.
--
-- Rule, all per Server/Header/S15_MyShare.cpp:2212-2268 (evaluated in this exact order):
--   * Value02 = 0 unconditionally accepts the record, ahead of every other rule (:2214-2215).
--   * Type = 0 (with Value02 <> 0) likewise unconditionally accepts (:2216-2217).
--   * Type outside 1-46 (neither bypass above applied) rejects (:2218).
--   * Value01's stated range check (:2220) is confirmed dead code -- it requires Value01 to be
--     simultaneously negative and > 10000, which no integer satisfies -- so it never rejects any record and
--     is deliberately NOT reproduced as a constraint here; there is no effective legacy rule to mirror.
--   * Type = 1: Value02 in 1-33, Value03 in 0-400, Value04 in 0-400 (:2222-2230).
--   * Type 2-29: Value02 in 1-100, Value03 in 0-1000, Value04 in 0-1000 (:2231-2239).
--   * Type 30-38: the band written to guard this window (:2240-2248) is also confirmed dead code -- it
--     requires Type to be simultaneously >= 30 and <= 28 -- so every record with Type in this window falls
--     through, unconstrained on Value02/Value03/Value04, to the final unconditional accept (:2267). This is
--     an unintentionally permissive gap, not a deliberate "no constraints" rule, and the true intended
--     bounds cannot be recovered from the source as written -- deliberately left unconstrained here to
--     match legacy's actual (buggy) runtime behavior rather than guess at bounds that cannot be cited.
--   * Type 39-42: Value02 in 1-10, Value03 >= 1 (no stated upper bound), Value04 = 0 (:2249-2257).
--   * Type 43-46: Value02 in 1-10, Value03 >= 6 (no stated upper bound), Value04 = 0 (:2258-2266).
-- All five Type-band rules interact with the same four columns per record, so this ships as one composite
-- CHECK rather than fragmenting into independent per-column constraints that could not express "which band
-- applies" on their own.
--
-- Existing seed data (Migrations/Seed/world/065_gem_sockets.sql, 2891 rows) checked against this
-- constraint before adding it as a validating (WITH CHECK, the ALTER TABLE default) constraint: 33 Type=1
-- rows (Value02 1-33, Value03 25-400, Value04 0-400), 2800 Type 2-29 rows (Value02 1-100, Value03
-- 105-1000, Value04 230-1000), 18 Type 30-38 rows (left unconstrained per above), 40 Type 39-42 rows
-- (Value02 1-10, Value03 1-10, Value04 = 0), 0 Type 43-46 rows, 0 rows using either zero-bypass -- all
-- comfortably satisfy the new constraint, so this migration cannot fail against the shipped dataset.
--
-- Deliberately NOT added here, following the same precedent Migrations/031 (QuestId) and Migrations/033
-- (NpcId) already established for this exact kind of static-data audit series:
--   * The total-decoded-record-count-must-equal-2891 invariant (Server/Header/S15_MyShare.cpp:723,744 and
--     Server/Header/Scope/ZlibScope.h:86-107) -- a whole-table cardinality invariant, not a per-row CHECK,
--     and GemSocketId is already boot-time, generator-assigned reference data (PRIMARY KEY / NOT NULL,
--     currently populated 1-2891 by
--     Tools/Fenrir.Tools.LegacyDataImport/Legacy/Seeding/GemSocketSeedGenerator.cs) rather than externally
--     supplied runtime input.
--   * The duplicate-(Type, Value02)-pair "later record silently unreachable at lookup time" behavior --
--     world.GemSockets already enforces UQ_GemSockets_Type_Value02, which is *stricter* than legacy
--     (legacy's load gate tolerates duplicates and only loses the later one at GSOCKET::Search time). This
--     is a pre-existing hardening choice, not something this script touches; flagged for reviewer awareness
--     that a future re-seed derived from source data containing a legal (per the legacy gate) duplicate
--     pair would be rejected here, where legacy itself would have silently accepted it.
ALTER TABLE world.GemSockets
    ADD CONSTRAINT CK_GemSockets_TypeBandRules CHECK (
        Value02 = 0
        OR Type = 0
        OR (
            Type BETWEEN 1 AND 46
            AND (
                (Type = 1 AND Value02 BETWEEN 1 AND 33 AND Value03 BETWEEN 0 AND 400 AND Value04 BETWEEN 0 AND 400)
                OR (Type BETWEEN 2 AND 29 AND Value02 BETWEEN 1 AND 100 AND Value03 BETWEEN 0 AND 1000 AND Value04 BETWEEN 0 AND 1000)
                OR (Type BETWEEN 30 AND 38) -- dead band in legacy source; deliberately left unconstrained, see header
                OR (Type BETWEEN 39 AND 42 AND Value02 BETWEEN 1 AND 10 AND Value03 >= 1 AND Value04 = 0)
                OR (Type BETWEEN 43 AND 46 AND Value02 BETWEEN 1 AND 10 AND Value03 >= 6 AND Value04 = 0)
            )
        )
    );
GO
