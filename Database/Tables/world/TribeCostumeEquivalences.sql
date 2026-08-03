CREATE TABLE world.TribeCostumeEquivalences
(
    GroupIndex TINYINT NOT NULL,
    TribeId    TINYINT NOT NULL,
    ItemId     INT     NOT NULL,
    CONSTRAINT PK_TribeCostumeEquivalences PRIMARY KEY CLUSTERED (GroupIndex, TribeId),
    CONSTRAINT CK_TribeCostumeEquivalences_TribeId CHECK (TribeId BETWEEN 0 AND 2),
    CONSTRAINT UQ_TribeCostumeEquivalences_Tribe_Item UNIQUE (TribeId, ItemId),
    CONSTRAINT FK_TribeCostumeEquivalences_Item FOREIGN KEY (ItemId) REFERENCES world.Items (ItemId)
);
