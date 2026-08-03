CREATE TABLE game.GuildMembers
(
    GuildId      INT          NOT NULL,
    CharacterId  INT          NOT NULL,
    Role         TINYINT      NOT NULL
        CONSTRAINT DF_GuildMembers_Role DEFAULT 0,
    CallName     NVARCHAR(4)  NOT NULL
        CONSTRAINT DF_GuildMembers_CallName DEFAULT N'',
    JoinedAtUtc  DATETIME2(3) NOT NULL
        CONSTRAINT DF_GuildMembers_JoinedAtUtc DEFAULT SYSUTCDATETIME(),
    UpdatedAtUtc DATETIME2(3) NOT NULL
        CONSTRAINT DF_GuildMembers_UpdatedAtUtc DEFAULT SYSUTCDATETIME(),
    CONSTRAINT PK_GuildMembers PRIMARY KEY CLUSTERED (GuildId, CharacterId),
    CONSTRAINT UQ_GuildMembers_CharacterId UNIQUE (CharacterId),
    CONSTRAINT CK_GuildMembers_Role CHECK (Role BETWEEN 0 AND 2),
    CONSTRAINT FK_GuildMembers_Guild FOREIGN KEY (GuildId) REFERENCES game.Guilds (GuildId),
    CONSTRAINT FK_GuildMembers_Character FOREIGN KEY (CharacterId) REFERENCES game.Characters (CharacterId)
);
