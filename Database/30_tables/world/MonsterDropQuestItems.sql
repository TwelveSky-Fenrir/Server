-- legacy mDropQuestItemInfo[2] -- modeled as a single [rate, itemId] PAIR (matching the Potion/Extra
-- convention), NOT as two independent scalar slots, even though the brief's suggested shape was a
-- generic 2-slot table: a full-population scan of all 1139 monsters found the two fields are always
-- both-zero (1115 monsters) or both-non-zero (24 monsters) together, never partial, and the second
-- field always lands in a tight, plausible quest-item-id band (901-978) while the first is always one
-- of {10000, 500000, 1000000} -- a rate-like value, same shape as the other drop rate columns.
-- ServerDocs/12_ts25zone/14_MyGame05.md: "DROP_QUEST_ITEM (item de quete conditionne a l'etat de quete
-- du joueur)" confirms this is an ITEM (world.Items), not a world.Quests reference. At most one row per
-- monster (the struct has no array of these, just one pair), so MonsterId alone is the PK -- no
-- SlotIndex. QuestItemId is NOT NULL: the item half is provably non-zero whenever a row exists.
CREATE TABLE world.MonsterDropQuestItems
(
    MonsterId   INT NOT NULL,
    DropRate    INT NOT NULL,
    QuestItemId INT NOT NULL,
    CONSTRAINT PK_MonsterDropQuestItems PRIMARY KEY CLUSTERED (MonsterId),
    CONSTRAINT FK_MonsterDropQuestItems_Monster FOREIGN KEY (MonsterId) REFERENCES world.Monsters (MonsterId),
    CONSTRAINT FK_MonsterDropQuestItems_Item FOREIGN KEY (QuestItemId) REFERENCES world.Items (ItemId)
);
