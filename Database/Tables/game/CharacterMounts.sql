CREATE TABLE game.CharacterMounts
(
    CharacterId INT     NOT NULL,
    Slot        TINYINT NOT NULL,
    ItemId      INT     NOT NULL,
    ExpActivity INT     NOT NULL
        CONSTRAINT DF_CharacterMounts_ExpActivity DEFAULT 0,
    Power       INT     NOT NULL
        CONSTRAINT DF_CharacterMounts_Power DEFAULT 0,
    CONSTRAINT PK_CharacterMounts PRIMARY KEY CLUSTERED (CharacterId, Slot),
    CONSTRAINT CK_CharacterMounts_Slot CHECK (Slot <= 9),
    CONSTRAINT CK_CharacterMounts_ItemId CHECK (ItemId >= 1),
    CONSTRAINT CK_CharacterMounts_ExpActivity CHECK (ExpActivity >= 0),
    CONSTRAINT CK_CharacterMounts_Power CHECK (Power >= 0),
    CONSTRAINT FK_CharacterMounts_Character FOREIGN KEY (CharacterId) REFERENCES game.Characters (CharacterId)
        ON DELETE CASCADE
);
