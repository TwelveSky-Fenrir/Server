-- Normalizes legacy `gMember`, a packed VARCHAR(850) blob of ~70 fixed-width GUILD_MEMBER_INFO records
-- (CSQLGuild.cpp GetGuildInfo, gMemberSize = MAX_GUILD_MEMBER_TEXT_LENGTH = MAX_AVATAR_NAME_LENGTH *
-- MAX_GUILD_AVATAR_NUM(50) + MAX_GUILD_AVATAR_NUM*sizeof(int) + 1) into one row per actually-occupied
-- slot -- "normalize, don't transliterate": most of the 50 slots are '@'-padded/empty on a typical guild,
-- so a wide 50-column table would be almost entirely NULL. A row simply not existing IS "slot empty".
-- Role encodes GUILD_MEMBER_INFO.gMemberRole (packed as an ASCII digit '0'/'1'/... in the blob):
--   0 = member, 1 = sub-master, 2 = master.
-- gMemberCall (a legacy MAX_CALL_NAME_LENGTH-1 = 4-char cosmetic in-guild nickname/title per member) is
-- deliberately NOT carried over: it is flavor text with no gameplay/protocol semantics beyond display,
-- out of scope for getting the guild membership schema right; add a nullable CallName column later if
-- the wire contract for the guild-info panel turns out to need it.
-- UNIQUE(CharacterId): a character is a member of at most one guild at a time (legacy has no mechanism
-- for a name to appear in two guilds' gMember blobs simultaneously either).
CREATE TABLE game.GuildMembers
(
    GuildId     INT     NOT NULL,
    CharacterId INT     NOT NULL,
    Role        TINYINT NOT NULL CONSTRAINT DF_GuildMembers_Role DEFAULT 0,
    JoinedAtUtc DATETIME2(3) NOT NULL CONSTRAINT DF_GuildMembers_JoinedAtUtc DEFAULT SYSUTCDATETIME(),
    CONSTRAINT PK_GuildMembers PRIMARY KEY CLUSTERED (GuildId, CharacterId),
    CONSTRAINT UQ_GuildMembers_CharacterId UNIQUE (CharacterId),
    CONSTRAINT CK_GuildMembers_Role CHECK (Role BETWEEN 0 AND 2),
    CONSTRAINT FK_GuildMembers_Guild FOREIGN KEY (GuildId) REFERENCES game.Guilds (GuildId),
    CONSTRAINT FK_GuildMembers_Character FOREIGN KEY (CharacterId) REFERENCES game.Characters (CharacterId)
);
