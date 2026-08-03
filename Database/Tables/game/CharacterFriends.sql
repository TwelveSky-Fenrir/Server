CREATE TABLE game.CharacterFriends
(
    CharacterId       INT          NOT NULL,
    Slot              TINYINT      NOT NULL,
    FriendCharacterId INT          NOT NULL,
    CreatedAtUtc      DATETIME2(3) NOT NULL
        CONSTRAINT DF_CharacterFriends_CreatedAtUtc DEFAULT SYSUTCDATETIME(),
    CONSTRAINT PK_CharacterFriends PRIMARY KEY CLUSTERED (CharacterId, Slot),
    CONSTRAINT CK_CharacterFriends_Slot CHECK (Slot BETWEEN 0 AND 9),
    CONSTRAINT FK_CharacterFriends_Character FOREIGN KEY (CharacterId) REFERENCES game.Characters (CharacterId),
    CONSTRAINT FK_CharacterFriends_FriendCharacter FOREIGN KEY (FriendCharacterId) REFERENCES game.Characters (CharacterId)
);
