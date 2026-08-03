CREATE TABLE game.CharacterBuffs
(
    CharacterId          INT     NOT NULL,
    SlotIndex            TINYINT NOT NULL,
    Value                INT     NOT NULL
        CONSTRAINT DF_CharacterBuffs_Value DEFAULT 0,
    RemainingLegacyTicks INT     NOT NULL
        CONSTRAINT DF_CharacterBuffs_RemainingLegacyTicks DEFAULT 0,
    CONSTRAINT PK_CharacterBuffs PRIMARY KEY CLUSTERED (CharacterId, SlotIndex),
    CONSTRAINT CK_CharacterBuffs_SlotIndex CHECK (SlotIndex <= 34),
    CONSTRAINT CK_CharacterBuffs_RemainingLegacyTicks CHECK (RemainingLegacyTicks >= 0),
    CONSTRAINT FK_CharacterBuffs_Character FOREIGN KEY (CharacterId) REFERENCES game.Characters (CharacterId)
);
