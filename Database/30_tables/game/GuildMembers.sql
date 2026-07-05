-- Normalizes legacy `gMember` (a packed VARCHAR(850) blob of ~70 GUILD_MEMBER_INFO records) into one row
-- per occupied slot -- a row simply not existing IS "slot empty". Role: 0 = member, 1 = sub-master, 2 = master.
-- CallName is the 4-char cosmetic in-guild title (gMemberCall, ZC_GUILD_WORK_RECV.MemberCallNames);
-- NVARCHAR(4) not 5 since Fenrir strings are never NUL-terminated in storage. UNIQUE(CharacterId): a
-- character is a member of at most one guild at a time.
CREATE TABLE game.GuildMembers
(
    GuildId     INT     NOT NULL,
    CharacterId INT     NOT NULL,
    Role        TINYINT NOT NULL CONSTRAINT DF_GuildMembers_Role DEFAULT 0,
    CallName    NVARCHAR(4) NOT NULL CONSTRAINT DF_GuildMembers_CallName DEFAULT N'',
    JoinedAtUtc DATETIME2(3) NOT NULL CONSTRAINT DF_GuildMembers_JoinedAtUtc DEFAULT SYSUTCDATETIME(),
    CONSTRAINT PK_GuildMembers PRIMARY KEY CLUSTERED (GuildId, CharacterId),
    CONSTRAINT UQ_GuildMembers_CharacterId UNIQUE (CharacterId),
    CONSTRAINT CK_GuildMembers_Role CHECK (Role BETWEEN 0 AND 2),
    CONSTRAINT FK_GuildMembers_Guild FOREIGN KEY (GuildId) REFERENCES game.Guilds (GuildId),
    CONSTRAINT FK_GuildMembers_Character FOREIGN KEY (CharacterId) REFERENCES game.Characters (CharacterId)
);
