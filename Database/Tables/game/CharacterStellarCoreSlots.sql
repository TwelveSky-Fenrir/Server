CREATE TABLE game.CharacterStellarCoreSlots
(
    CharacterId INT     NOT NULL,
    Slot        TINYINT NOT NULL,
    ItemId      INT     NOT NULL,
    CONSTRAINT PK_CharacterStellarCoreSlots PRIMARY KEY CLUSTERED (CharacterId, Slot),
    CONSTRAINT CK_CharacterStellarCoreSlots_Slot CHECK (Slot <= 9),
    CONSTRAINT CK_CharacterStellarCoreSlots_ItemId CHECK (ItemId >= 1),
    CONSTRAINT FK_CharacterStellarCoreSlots_Character FOREIGN KEY (CharacterId) REFERENCES game.Characters (CharacterId)
        ON DELETE CASCADE
);
