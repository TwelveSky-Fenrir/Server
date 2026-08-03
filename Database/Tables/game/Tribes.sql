CREATE TABLE game.Tribes
(
    TribeId           TINYINT NOT NULL,
    MasterCharacterId INT     NULL,
    CONSTRAINT PK_Tribes PRIMARY KEY CLUSTERED (TribeId),
    CONSTRAINT CK_Tribes_TribeId CHECK (TribeId BETWEEN 0 AND 3),
    CONSTRAINT FK_Tribes_MasterCharacter FOREIGN KEY (MasterCharacterId) REFERENCES game.Characters (CharacterId)
);
