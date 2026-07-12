-- Legacy mDropExtraItemInfo[50][2], the per-monster loot table; one row per populated slot (rate/id pair not both zero), including partial pairs (rate-only or item-only) that real data actually has.
CREATE TABLE world.MonsterDropExtraItems
(
    MonsterId INT     NOT NULL,
    SlotIndex TINYINT NOT NULL,                                                                      -- 0-49, position within mDropExtraItemInfo -- not a meaningful game id, just array position
    DropRate  INT     NOT NULL,
    ItemId    INT     NULL,                                                                          -- NULL when the legacy slot's item half was 0 (rate set, no item wired up)
    CONSTRAINT PK_MonsterDropExtraItems PRIMARY KEY CLUSTERED (MonsterId, SlotIndex),
    CONSTRAINT FK_MonsterDropExtraItems_Monster FOREIGN KEY (MonsterId) REFERENCES world.Monsters (MonsterId),
    CONSTRAINT FK_MonsterDropExtraItems_Item FOREIGN KEY (ItemId) REFERENCES world.Items (ItemId),
    CONSTRAINT CK_MonsterDropExtraItems_SlotIndex CHECK (SlotIndex BETWEEN 0 AND 49),
    -- Value-range bounds mirror legacy's load-time field validation (Monster_CheckValidElement,
    -- Server/Header/S15_MyShare.cpp:1819-1830). ItemId stays nullable-aware (rate-only slots are real seeded
    -- data -- see the table's own header comment).
    CONSTRAINT CK_MonsterDropExtraItems_DropRate CHECK (DropRate BETWEEN 0 AND 1000000),             -- :1819-1825
    CONSTRAINT CK_MonsterDropExtraItems_ItemId CHECK (ItemId IS NULL OR ItemId BETWEEN 0 AND 99999)  -- :1826-1830
    -- No secondary index on ItemId: verified repo-wide that the only reader of this table
    -- (usp_Monster_GetDrops) scans it unfiltered, ORDER BY MonsterId, SlotIndex -- already covered by the
    -- clustered PK. A reverse "which monsters drop item X" index would only tax the one-time seed insert
    -- (Migrations/Seed/world/090_monsters.sql) for zero read benefit today; re-add if that GM-tooling query
    -- is ever built.
);
