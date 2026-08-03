CREATE TABLE game.CharacterLogoutState
(
    CharacterId   INT          NOT NULL,
    LastZone      INT          NOT NULL
        CONSTRAINT DF_CharacterLogoutState_LastZone DEFAULT 0,
    PosX          INT          NOT NULL
        CONSTRAINT DF_CharacterLogoutState_PosX DEFAULT 0,
    PosY          INT          NOT NULL
        CONSTRAINT DF_CharacterLogoutState_PosY DEFAULT 0,
    PosZ          INT          NOT NULL
        CONSTRAINT DF_CharacterLogoutState_PosZ DEFAULT 0,
    Life          INT          NOT NULL
        CONSTRAINT DF_CharacterLogoutState_Life DEFAULT 0,
    Mana          INT          NOT NULL
        CONSTRAINT DF_CharacterLogoutState_Mana DEFAULT 0,
    FlushSequence BIGINT       NOT NULL
        CONSTRAINT DF_CharacterLogoutState_FlushSequence DEFAULT 0,
    CapturedAtUtc DATETIME2(3) NOT NULL,
    CONSTRAINT PK_CharacterLogoutState PRIMARY KEY CLUSTERED (CharacterId),
    CONSTRAINT FK_CharacterLogoutState_Character FOREIGN KEY (CharacterId) REFERENCES game.Characters (CharacterId)
        ON DELETE CASCADE
);
