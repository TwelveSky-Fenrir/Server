-- Quest static-data load-time field validation (legacy Load_Quest / Quest_CheckValidElement) --
-- fenrir-database-engineer behavior contract, closes the two items its Edge-cases section marks as
-- "confirmed unreplicated ... concrete gap" (all other legacy Quest_CheckValidElement bounds are already
-- covered by Category/Type/Sort CHECKs on world.Quests, the FK-based checks on
-- Level/SummonZoneNumber/Start-End-KeyNPCNumber/NextIndex, or are deliberately unchecked in both legacy and
-- SQL alike -- see the contract's Edge cases section for the full item-by-item disposition).
--
-- Legacy never lets an out-of-range Step or reward Amount reach the shared-memory quest table at all:
-- Quest_CheckValidElement (Server/Header/S15_MyShare.cpp:2003-2007 for Step, :2044-2054 for reward
-- Amount) rejects the whole 1000-slot Load_Quest call on the first offending record
-- (Server/Header/S15_MyShare.cpp:670-719), which is fatal to ts25sharemem's boot sequence
-- (Server/ts25sharemem/main.cpp:87-93). A relational CHECK constraint is the closest available Fenrir
-- equivalent of "this value can never be stored" -- stricter than legacy in one respect (enforced on every
-- write, not just at one boot-time pass) but matching legacy's intent that these two fields are bounded,
-- which today's world.Quests / world.QuestRewards schema does not enforce at all
-- (Database/Tables/world/Quests.sql:43-45 lists CK_Quests_Category/CK_Quests_Type/CK_Quests_Sort only;
-- Database/Tables/world/QuestRewards.sql:14-19 lists CK_QuestRewards_SlotIndex/
-- CK_QuestRewards_RewardType/CK_QuestRewards_ItemXorAmount only -- neither table has ever had a Step or
-- Amount range check).
--
-- New scripts, not edits to the already-applied Tables/world/Quests.sql / Tables/world/QuestRewards.sql
-- (DbMigrator journals every applied script by SHA-256 and refuses a changed re-apply).
--
-- Existing seed data (Migrations/Seed/world/050_quests.sql, 051_quest_rewards.sql) was checked against
-- both new bounds before adding these as validating (WITH CHECK, the ALTER TABLE default) constraints:
-- Step ranges 1-207 across all 688 seeded quests, and reward Amount ranges 3-5,847,771 across all 1,436
-- seeded reward slots -- both comfortably inside the new bounds, so this migration cannot fail against the
-- shipped dataset.
--
-- Deliberately NOT added here: a CHECK (QuestId BETWEEN 1 AND 1000) on world.Quests. The contract's Edge
-- cases section discusses this as an "unreplicated legacy bound" too but explicitly ranks it lower
-- severity than Step/Amount and does not tag it "a concrete gap" the way it tags these two, because
-- QuestId is boot-time, generator-assigned reference data (already PRIMARY KEY / NOT NULL, currently
-- populated 1-688) rather than externally supplied input -- left out of scope for this script.
ALTER TABLE world.Quests
    ADD CONSTRAINT CK_Quests_Step CHECK (Step BETWEEN 1 AND 1000);
GO

ALTER TABLE world.QuestRewards
    ADD CONSTRAINT CK_QuestRewards_Amount CHECK (Amount IS NULL OR Amount BETWEEN 0 AND 100000000);
GO
