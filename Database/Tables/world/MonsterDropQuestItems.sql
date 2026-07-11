-- Legacy mDropQuestItemInfo[2], modeled as a single [rate, itemId] pair (fields are always both-zero or both-non-zero together).
-- References world.Items, not world.Quests (ServerDocs/12_ts25zone/14_MyGame05.md: "item de quete conditionne a l'etat de quete du joueur").
CREATE TABLE world.MonsterDropQuestItems
(
    MonsterId   INT NOT NULL,
    DropRate    INT NOT NULL,
    QuestItemId INT NOT NULL,
    CONSTRAINT PK_MonsterDropQuestItems PRIMARY KEY CLUSTERED (MonsterId),
    CONSTRAINT FK_MonsterDropQuestItems_Monster FOREIGN KEY (MonsterId) REFERENCES world.Monsters (MonsterId),
    CONSTRAINT FK_MonsterDropQuestItems_Item FOREIGN KEY (QuestItemId) REFERENCES world.Items (ItemId),
    -- Value-range bounds mirror legacy's load-time field validation (Monster_CheckValidElement,
    -- Server/Header/S15_MyShare.cpp:1809-1818). This table had never had any CHECK constraint at all
    -- (single-row-per-monster shape, no SlotIndex to bound).
    CONSTRAINT CK_MonsterDropQuestItems_DropRate CHECK (DropRate BETWEEN 0 AND 1000000), -- :1809-1813
    CONSTRAINT CK_MonsterDropQuestItems_QuestItemId CHECK (QuestItemId BETWEEN 0 AND 99999) -- :1814-1818
);
