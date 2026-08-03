CREATE TABLE world.TribeItemEquivalences
(
    GroupIndex TINYINT NOT NULL,
    TribeId    TINYINT NOT NULL,
    ItemId     INT     NOT NULL,
    CONSTRAINT PK_TribeItemEquivalences PRIMARY KEY CLUSTERED (GroupIndex, TribeId),
    CONSTRAINT CK_TribeItemEquivalences_TribeId CHECK (TribeId BETWEEN 0 AND 2),
    CONSTRAINT UQ_TribeItemEquivalences_Tribe_Item UNIQUE (TribeId, ItemId),
    CONSTRAINT FK_TribeItemEquivalences_Item FOREIGN KEY (ItemId) REFERENCES world.Items (ItemId)
);
