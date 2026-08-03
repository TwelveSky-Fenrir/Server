CREATE TABLE game.CharacterCostumes
(
    CharacterId INT     NOT NULL,
    Slot        TINYINT NOT NULL,
    ItemId      INT     NOT NULL,
    ItemDate    INT     NOT NULL
        CONSTRAINT DF_CharacterCostumes_ItemDate DEFAULT 0,
    ExpireDate  INT     NOT NULL
        CONSTRAINT DF_CharacterCostumes_ExpireDate DEFAULT 0,
    CONSTRAINT PK_CharacterCostumes PRIMARY KEY CLUSTERED (CharacterId, Slot),
    CONSTRAINT CK_CharacterCostumes_Slot CHECK (Slot <= 9),
    CONSTRAINT CK_CharacterCostumes_ItemId CHECK (ItemId >= 1),
    CONSTRAINT FK_CharacterCostumes_Character FOREIGN KEY (CharacterId) REFERENCES game.Characters (CharacterId)
);
