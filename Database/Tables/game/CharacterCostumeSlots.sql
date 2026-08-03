CREATE TABLE game.CharacterCostumeSlots
(
    CharacterId  INT     NOT NULL,
    Slot         TINYINT NOT NULL,
    ItemId       INT     NOT NULL,
    EnchantValue INT     NOT NULL
        CONSTRAINT DF_CharacterCostumeSlots_EnchantValue DEFAULT 0,
    ExpireDate   INT     NOT NULL
        CONSTRAINT DF_CharacterCostumeSlots_ExpireDate DEFAULT 0,
    CONSTRAINT PK_CharacterCostumeSlots PRIMARY KEY CLUSTERED (CharacterId, Slot),
    CONSTRAINT CK_CharacterCostumeSlots_Slot CHECK (Slot <= 9),
    CONSTRAINT CK_CharacterCostumeSlots_ItemId CHECK (ItemId >= 1),
    CONSTRAINT FK_CharacterCostumeSlots_Character FOREIGN KEY (CharacterId) REFERENCES game.Characters (CharacterId),
    CONSTRAINT FK_CharacterCostumeSlots_World_Item FOREIGN KEY (ItemId) REFERENCES world.Items (ItemId)
);
