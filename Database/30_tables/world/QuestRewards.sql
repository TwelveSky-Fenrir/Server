-- Normalized from qReward[3][2] (see world.Quests header comment for the full derivation). Confirmed from the
-- actual game logic that qReward[slot][0] is a RewardType discriminator, NOT an itemId -- the task brief's
-- naive (itemId, quantity) pairing assumption does not hold, so this deliberately deviates from that shape:
--   2=Money (added to aMoney), 3=KillOtherTribeCount (a PK/faction-kill stat), 4=Experience, 5=TeacherPoint,
--   6=Item (Value is an ItemId -- ReturnItemQuantityForQuestReward in ts25zone/S07_MyGame04.cpp proves the
--   granted quantity is derived at grant-time from the item's own iSort, equipment=>0/other=>1, and is never
--   itself stored here).
-- RewardType=1 ("none") is a deliberate no-op sentinel (case 1: break in the reward-grant switch) used to
-- pad the two optional slots -- those rows are never stored (only 1434 of a theoretical 688 x 3 = 2064 slots
-- are populated; slot 0 is always populated, slot 1 in 517/688 quests, slot 2 in 229/688 quests).
-- ItemId/Amount are mutually exclusive by RewardType, not two independent nullable columns: ItemId is set
-- only for RewardType=6 (and FK's to world.Items), Amount for everything else -- enforced by
-- CK_QuestRewards_ItemXorAmount rather than trusting the generator alone.
CREATE TABLE world.QuestRewards
(
    QuestId    INT     NOT NULL,
    SlotIndex  TINYINT NOT NULL, -- 0..2, legacy qReward row position
    RewardType TINYINT NOT NULL, -- legacy qReward[slot][0], see header comment
    ItemId     INT NULL,         -- legacy qReward[slot][1] as an ItemId; RewardType=6 only
    Amount     INT NULL,         -- legacy qReward[slot][1] as a scalar amount; RewardType IN (2,3,4,5)
    CONSTRAINT PK_QuestRewards PRIMARY KEY CLUSTERED (QuestId, SlotIndex),
    CONSTRAINT FK_QuestRewards_Quest FOREIGN KEY (QuestId) REFERENCES world.Quests (QuestId),
    CONSTRAINT FK_QuestRewards_Item FOREIGN KEY (ItemId) REFERENCES world.Items (ItemId),
    CONSTRAINT CK_QuestRewards_SlotIndex CHECK (SlotIndex BETWEEN 0 AND 2),
    CONSTRAINT CK_QuestRewards_RewardType CHECK (RewardType BETWEEN 2 AND 6),
    CONSTRAINT CK_QuestRewards_ItemXorAmount CHECK (
        (RewardType = 6 AND ItemId IS NOT NULL AND Amount IS NULL) OR
        (RewardType <> 6 AND ItemId IS NULL AND Amount IS NOT NULL)
        )
);
