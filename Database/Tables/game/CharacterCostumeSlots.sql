CREATE TABLE game.CharacterCostumeSlots
(
    CharacterId INT     NOT NULL,
    Slot        TINYINT NOT NULL,
    ItemId      INT     NOT NULL,
    ItemValue   INT     NOT NULL
        CONSTRAINT DF_CharacterCostumeSlots_ItemValue DEFAULT 0,
    ExpireDate  INT     NOT NULL
        CONSTRAINT DF_CharacterCostumeSlots_ExpireDate DEFAULT 0,
    CONSTRAINT PK_CharacterCostumeSlots PRIMARY KEY CLUSTERED (CharacterId, Slot),
    CONSTRAINT CK_CharacterCostumeSlots_Slot CHECK (Slot <= 9),
    CONSTRAINT CK_CharacterCostumeSlots_ItemId CHECK (ItemId >= 1),
    CONSTRAINT FK_CharacterCostumeSlots_Character FOREIGN KEY (CharacterId) REFERENCES game.Characters (CharacterId)
        ON DELETE CASCADE,
    CONSTRAINT FK_CharacterCostumeSlots_World_Item FOREIGN KEY (ItemId) REFERENCES world.Items (ItemId)
);
