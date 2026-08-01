-- Seeds world.RewardBundles/world.RewardBundleItems with the one concrete, cited, confirmed-read reward
-- table legacy actually ever sends to a client: `ts25extra`'s ZONE_GET_REWARD_ITEM_FOR_EXTRA_SEND handler
-- reads the legacy `rewardinfo` MySQL table via MyDB::GetRewardItem, then unconditionally DISCARDS that
-- result and overwrites both the result flag and all 7 item slots with a fixed, hardcoded table before
-- responding (Server/ts25extra/S04_MyWork02.cpp:1352-1372, confirmed read -- see the fully-cited
-- legacy-behavior-translator contract for D1-reward-fetch, wave10, for the complete op27/GET_REWARD_ITEM
-- trace). That fixed table, reproduced verbatim below (Server/ts25extra/S04_MyWork02.cpp:1364-1370,
-- confirmed read), is therefore the only reward-item data with any real evidentiary basis anywhere in the
-- legacy source; the genuine `rewardinfo`-table read exists in the source but its result is provably never
-- observed by any client in the shipped build.
--
-- All 6 distinct item ids below (613, 8406, 8411, 8413, 8414, 8420) already exist in world.Items
-- (005_items.sql) under names matching the source's own inline day-labeling comments, which independently
-- corroborates this is the correct table (e.g. 613 = "Absorption Pill (S)" for the source's "Day 4:
-- Absorption Pill(S)"; 8420 = "Premium Service(1d)" for "Day 2: Premium Service(1d)").
--
-- Open question preserved, not resolved here: Day 1 and Day 6 both resolve to item 8406 in the cited
-- source (Server/ts25extra/S04_MyWork02.cpp:1364,1369) even though the source's own comment labels Day 6
-- "Double Mount EXP" -- a different reward than Day 1's "Auto Buff Scroll(7d)". Whether this is an
-- intentional repeated reward or a legacy authoring mistake could not be determined from the cited code;
-- reproduced verbatim (not corrected to a guessed distinct id) per that contract's own explicit
-- non-assertion either way.
--
-- Whether "reproduce the fixed table" is the right long-term design versus "restore the genuine
-- rewardinfo-driven query the scaffolding implies" is flagged by that contract as an unresolved product
-- decision, not settled by this script. This script only seeds the sole cited, confirmed-read value set
-- available -- it does not touch the surrounding data-store-backed architecture (world.RewardBundles /
-- world.RewardBundleItems / IWorldDataRepository), which is Fenrir's own, already-shipped, idiomatic
-- reimplementation choice and is left exactly as-is.
IF
    NOT EXISTS (SELECT 1
                FROM world.RewardBundles)
    BEGIN
        INSERT INTO world.RewardBundles (RewardBundleId)
        VALUES (1);

        INSERT INTO world.RewardBundleItems (RewardBundleId, SlotIndex, ItemId)
        VALUES (1, 1, 8406), -- Day 1: Auto Buff Scroll(7d)
               (1, 2, 8420), -- Day 2: Premium Service(1d)
               (1, 3, 8411), -- Day 3: SkyLord's Blessed Feed
               (1, 4, 613),  -- Day 4: Absorption Pill(S)
               (1, 5, 8414), -- Day 5: EXP Booster(L) (source comment: "EXP Booster / VOD-like reward")
               (1, 6, 8406), -- Day 6: same item id as Day 1 in the cited source; preserved verbatim, not corrected
               (1, 7, 8413); -- Day 7: EXP Boost [Pet] (source comment: "Pet EXP boost pill")
    END;
