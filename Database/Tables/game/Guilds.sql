-- Legacy `guildinfo` is keyed on gName (no surrogate id in the wire protocol); GuildId is minted here so
-- GuildMembers/GuildNotices can FK onto a stable surrogate, with UQ_Guilds_Name preserving the legacy
-- "name is the real key" guarantee. MasterCharacterId replaces the legacy gMaster01 VARCHAR name with a
-- real FK. Guild sub-master info lives per-member as GuildMembers.Role, not as columns here.
CREATE TABLE game.Guilds
(
    GuildId           INT IDENTITY (1,1) NOT NULL,
    Name              NVARCHAR(12)       NOT NULL,      -- legacy gName, MAX_GUILD_NAME_LENGTH=13 (12 chars + NUL)
    Grade             INT                NOT NULL
        CONSTRAINT DF_Guilds_Grade DEFAULT 0,           -- gGrade
    MasterCharacterId INT                NULL,          -- gMaster01, resolved to a real character
    Points            INT                NOT NULL
        CONSTRAINT DF_Guilds_Points DEFAULT 0,          -- gPoint
    BuffType          INT                NOT NULL
        CONSTRAINT DF_Guilds_BuffType DEFAULT 0,        -- gBuffType
    BuffState         INT                NOT NULL
        CONSTRAINT DF_Guilds_BuffState DEFAULT 0,       -- gBuffState
    BuffTime          INT                NOT NULL
        CONSTRAINT DF_Guilds_BuffTime DEFAULT 0,        -- gBuffTime
    BuffTimeForDiff   BIGINT             NOT NULL
        CONSTRAINT DF_Guilds_BuffTimeForDiff DEFAULT 0, -- gBuffTimeForDiff (bigint(19) in the legacy dump)
    Logo              INT                NOT NULL
        CONSTRAINT DF_Guilds_Logo DEFAULT 0,            -- gLogo
    CreatedAtUtc      DATETIME2(3)       NOT NULL
        CONSTRAINT DF_Guilds_CreatedAtUtc DEFAULT SYSUTCDATETIME(),
    UpdatedAtUtc      DATETIME2(3)       NOT NULL
        CONSTRAINT DF_Guilds_UpdatedAtUtc DEFAULT SYSUTCDATETIME(), -- bumped by every Grade/Points/Buff*/Logo mutation, matching Characters/AccountVault/AccountCash/WorldState/GuildNotices
    CONSTRAINT PK_Guilds PRIMARY KEY CLUSTERED (GuildId),
    CONSTRAINT UQ_Guilds_Name UNIQUE (Name),
    CONSTRAINT FK_Guilds_MasterCharacter FOREIGN KEY (MasterCharacterId) REFERENCES game.Characters (CharacterId),
    INDEX IX_Guilds_MasterCharacter NONCLUSTERED (MasterCharacterId)
);
