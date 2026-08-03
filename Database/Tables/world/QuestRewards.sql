CREATE TABLE world.QuestRewards
(
    QuestId    INT     NOT NULL,
    SlotIndex  TINYINT NOT NULL,
    RewardType TINYINT NOT NULL,
    ItemId     INT     NULL,
    Amount     INT     NULL,
    CONSTRAINT PK_QuestRewards PRIMARY KEY CLUSTERED (QuestId, SlotIndex),
    CONSTRAINT FK_QuestRewards_Quest FOREIGN KEY (QuestId) REFERENCES world.Quests (QuestId),
    CONSTRAINT FK_QuestRewards_Item FOREIGN KEY (ItemId) REFERENCES world.Items (ItemId),
    CONSTRAINT CK_QuestRewards_SlotIndex CHECK (SlotIndex BETWEEN 0 AND 2),
    CONSTRAINT CK_QuestRewards_RewardType CHECK (RewardType BETWEEN 2 AND 6),
    CONSTRAINT CK_QuestRewards_ItemXorAmount CHECK (
        (RewardType = 6 AND ItemId IS NOT NULL AND Amount IS NULL) OR
        (RewardType <> 6 AND ItemId IS NULL AND Amount IS NOT NULL)
        ),
    CONSTRAINT CK_QuestRewards_Amount CHECK (Amount IS NULL OR Amount BETWEEN 0 AND 100000000)
);
