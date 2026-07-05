-- Legacy: aFriend[10] (MAX_FRIEND_NUM); one row per occupied slot, Slot is the client-chosen index.
-- ONE-DIRECTIONAL by design (legacy: only the sender adds the friend): a row says "CharacterId considers
-- FriendCharacterId a friend"; the reverse is a separate row or may not exist.
CREATE TABLE game.CharacterFriends
(
    CharacterId       INT     NOT NULL,
    Slot              TINYINT NOT NULL,
    FriendCharacterId INT     NOT NULL,
    CreatedAtUtc      DATETIME2(3) NOT NULL CONSTRAINT DF_CharacterFriends_CreatedAtUtc DEFAULT SYSUTCDATETIME(),
    CONSTRAINT PK_CharacterFriends PRIMARY KEY CLUSTERED (CharacterId, Slot),
    CONSTRAINT CK_CharacterFriends_Slot CHECK (Slot BETWEEN 0 AND 9),
    CONSTRAINT FK_CharacterFriends_Character FOREIGN KEY (CharacterId) REFERENCES game.Characters (CharacterId),
    CONSTRAINT FK_CharacterFriends_FriendCharacter FOREIGN KEY (FriendCharacterId) REFERENCES game.Characters (CharacterId)
);
