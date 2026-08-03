CREATE TABLE world.RewardBundleItems
(
    RewardBundleId INT     NOT NULL,
    SlotIndex      TINYINT NOT NULL,
    ItemId         INT     NULL,
    CONSTRAINT PK_RewardBundleItems PRIMARY KEY CLUSTERED (RewardBundleId, SlotIndex),
    CONSTRAINT CK_RewardBundleItems_SlotIndex CHECK (SlotIndex BETWEEN 1 AND 7),
    CONSTRAINT FK_RewardBundleItems_RewardBundles FOREIGN KEY (RewardBundleId) REFERENCES world.RewardBundles (RewardBundleId),
    CONSTRAINT FK_RewardBundleItems_Items FOREIGN KEY (ItemId) REFERENCES world.Items (ItemId)
);
