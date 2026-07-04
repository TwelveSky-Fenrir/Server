-- Normalizes legacy `aFriend[MAX_FRIEND_NUM]` (MAX_FRIEND_NUM=10, DEFINE.h:305) -- a real 10-slot fixed
-- array per character -- into one row per occupied slot, same "normalize, don't transliterate" rule as
-- game.GuildMembers/game.TribeSubMasters (>4 slots -> child table) rather than 10 nullable columns.
-- Slot is the legacy client-chosen slot index (CZ_FRIEND_MAKE_SEND's tIndex, contracts/05_social.md):
-- the CLIENT picks which of its own 10 slots a new friend lands in, not the server, so it must be an
-- explicit column (not derived from insertion order), same reasoning as game.Characters.Slot.
-- ONE-DIRECTIONAL by design (verified, contracts/05_social.md CZ_FRIEND_MAKE_SEND note: "seul l'emetteur
-- ajoute l'ami -- l'autre joueur doit envoyer son propre FRIEND_MAKE") -- a row here says "CharacterId
-- considers FriendCharacterId a friend"; the reverse is a SEPARATE row (or may not exist at all). No
-- UNIQUE(CharacterId, FriendCharacterId): the legacy has no such guard either (a client could in theory
-- send the same FriendMake twice into two different slots), and nothing downstream relies on its absence
-- -- CZ_FRIEND_ASK_SEND's own "MAX_FRIEND_NUM=10 pleine ou doublon => Quit()" pre-check is enforced
-- app-side (FriendRegistry), exactly like every other social precondition in this lot.
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
