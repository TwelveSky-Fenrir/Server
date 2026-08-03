CREATE TABLE game.CharacterCostumes
(
    CharacterId INT     NOT NULL,
    Slot        TINYINT NOT NULL,
    ItemId      INT     NOT NULL,
    CONSTRAINT PK_CharacterCostumes PRIMARY KEY CLUSTERED (CharacterId, Slot),
    CONSTRAINT CK_CharacterCostumes_Slot CHECK (Slot <= 9),
    CONSTRAINT CK_CharacterCostumes_ItemId CHECK (ItemId >= 1),
    CONSTRAINT FK_CharacterCostumes_Character FOREIGN KEY (CharacterId) REFERENCES game.Characters (CharacterId)
);
