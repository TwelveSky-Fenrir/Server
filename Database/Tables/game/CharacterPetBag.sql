CREATE TABLE game.CharacterPetBag
(
    CharacterId INT     NOT NULL,
    Slot        TINYINT NOT NULL,
    ItemId      INT     NOT NULL,
    CONSTRAINT PK_CharacterPetBag PRIMARY KEY CLUSTERED (CharacterId, Slot),
    CONSTRAINT CK_CharacterPetBag_Slot CHECK (Slot <= 19),
    CONSTRAINT CK_CharacterPetBag_ItemId CHECK (ItemId >= 1),
    CONSTRAINT FK_CharacterPetBag_Character FOREIGN KEY (CharacterId) REFERENCES game.Characters (CharacterId)
);
