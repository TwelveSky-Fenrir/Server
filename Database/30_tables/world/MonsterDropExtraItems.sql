-- legacy mDropExtraItemInfo[50][2] -- THE real per-monster loot table (ServerDocs/12_ts25zone/
-- 14_MyGame05.md: "DROP_EXTRA_ITEM, 50 emplacements, avec une liste blanche #ifdef LNW33 d'objets
-- autorises"). Pair order [rate, itemId] cross-checked against Server/BuildEU33/005_00004DROP.CSV
-- field-for-field (e.g. monster 1 "Kobold" slot 6 = (50,1027), matching the CSV exactly).
-- Normalized to one row per POPULATED slot only (13686 of 56950 possible slots across 1139 monsters;
-- of those, 12970 carry a non-null ItemId and 716 are rate-only rows, see below).
-- Unlike MonsterDropPotions, a full-population scan found real partial-zero pairs here:
--   * 8013 slots have rate=0 with a non-zero item id -- several of these are byte-identical across many
--     early-game monsters (same item ids/slot positions), consistent with the LNW33 shared "whitelist"
--     baseline template mentioned above being baked into each record rather than looked up centrally.
--   * 716 slots have a non-zero rate but item id 0 (a roll with nothing to award).
-- "Populated" here means "not both zero" (so both of the above partial cases are kept, not just the
-- fully-paired ones) -- this preserves the legacy data losslessly rather than guessing which half is
-- the "real" one. ItemId is nullable and 0 is translated to NULL per the cross-domain FK contract
-- (0 is not a valid world.Items row).
CREATE TABLE world.MonsterDropExtraItems
(
    MonsterId INT     NOT NULL,
    SlotIndex TINYINT NOT NULL, -- 0-49, position within mDropExtraItemInfo -- not a meaningful game id, just array position
    DropRate  INT     NOT NULL,
    ItemId    INT NULL,         -- NULL when the legacy slot's item half was 0 (rate set, no item wired up)
    CONSTRAINT PK_MonsterDropExtraItems PRIMARY KEY CLUSTERED (MonsterId, SlotIndex),
    CONSTRAINT FK_MonsterDropExtraItems_Monster FOREIGN KEY (MonsterId) REFERENCES world.Monsters (MonsterId),
    CONSTRAINT FK_MonsterDropExtraItems_Item FOREIGN KEY (ItemId) REFERENCES world.Items (ItemId),
    CONSTRAINT CK_MonsterDropExtraItems_SlotIndex CHECK (SlotIndex BETWEEN 0 AND 49)
);
