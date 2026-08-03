CREATE TABLE game.CharacterRunes
(
    CharacterId INT     NOT NULL,
    SocketIndex TINYINT NOT NULL,
    RuneItemId  INT     NOT NULL,
    RuneStat    INT     NOT NULL
        CONSTRAINT DF_CharacterRunes_RuneStat DEFAULT 0,
    CONSTRAINT PK_CharacterRunes PRIMARY KEY CLUSTERED (CharacterId, SocketIndex),
    CONSTRAINT CK_CharacterRunes_SocketIndex CHECK (SocketIndex <= 3), 
    CONSTRAINT CK_CharacterRunes_RuneItemId CHECK (RuneItemId > 0),    
    CONSTRAINT FK_CharacterRunes_Character FOREIGN KEY (CharacterId) REFERENCES game.Characters (CharacterId)
);
