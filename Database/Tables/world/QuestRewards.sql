-- Normalized from qReward[3][2]; qReward[slot][0] is a RewardType discriminator, NOT an itemId:
--   2=Money, 3=KillOtherTribeCount, 4=Experience, 5=TeacherPoint, 6=Item (Value=ItemId), 1=no-op padding sentinel.
-- ItemId/Amount are mutually exclusive by RewardType (CK_QuestRewards_ItemXorAmount), not independent nullable columns.
CREATE TABLE world.QuestRewards
(
    QuestId    INT     NOT NULL,
    SlotIndex  TINYINT NOT NULL, -- 0..2, legacy qReward row position
    RewardType TINYINT NOT NULL, -- legacy qReward[slot][0], see header comment
    ItemId     INT     NULL,     -- legacy qReward[slot][1] as an ItemId; RewardType=6 only
    Amount     INT     NULL,     -- legacy qReward[slot][1] as a scalar amount; RewardType IN (2,3,4,5)
    CONSTRAINT PK_QuestRewards PRIMARY KEY CLUSTERED (QuestId, SlotIndex),
    CONSTRAINT FK_QuestRewards_Quest FOREIGN KEY (QuestId) REFERENCES world.Quests (QuestId),
    CONSTRAINT FK_QuestRewards_Item FOREIGN KEY (ItemId) REFERENCES world.Items (ItemId),
    CONSTRAINT CK_QuestRewards_SlotIndex CHECK (SlotIndex BETWEEN 0 AND 2),
    CONSTRAINT CK_QuestRewards_RewardType CHECK (RewardType BETWEEN 2 AND 6),
    CONSTRAINT CK_QuestRewards_ItemXorAmount CHECK (
        (RewardType = 6 AND ItemId IS NOT NULL AND Amount IS NULL) OR
        (RewardType <> 6 AND ItemId IS NULL AND Amount IS NOT NULL)
        ),
    -- Legacy never lets an out-of-range reward Amount reach the shared-memory quest table at all
    -- (Quest_CheckValidElement, Server/Header/S15_MyShare.cpp:2044-2054) -- rejects the whole Load_Quest
    -- call on the first offending record. Reward Amount ranges 3-5,847,771 across all 1,436 seeded reward
    -- slots (Migrations/Seed/world/023_quest_rewards.sql), comfortably inside this bound.
    CONSTRAINT CK_QuestRewards_Amount CHECK (Amount IS NULL OR Amount BETWEEN 0 AND 100000000)
);
