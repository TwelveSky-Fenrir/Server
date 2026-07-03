-- Legacy `guildinfo` (nxtserver.sql) is keyed on gName (PRIMARY KEY (`gName`)) -- guild identity really
-- is the name in ts25, there is no surrogate id anywhere in the wire protocol or the DB. We still mint
-- a real IDENTITY GuildId here so GuildMembers/GuildNotices can FK onto a stable, never-reused surrogate
-- (renaming a guild, if ever supported, must not orphan its members/notices) -- but UQ_Guilds_Name keeps
-- the legacy "name is the real key" guarantee intact.
-- gMaster01/gSubMaster01/gSubMaster02 are legacy VARCHAR(12) *names*; MasterCharacterId is a real FK to
-- game.Characters instead (a name is not a stable reference). Sub-master slots are NOT columns here --
-- unlike Guilds' own name-only sub-masters, CSQLWorldTribe.cpp/STRUCT.h show a guild has none of its own
-- (guild sub-master info lives per-member as GuildMembers.Role, see that table).
-- BuffType/BuffState/BuffTime/BuffTimeForDiff mirror gBuffType/gBuffState/gBuffTime/gBuffTimeForDiff
-- (guild-wide temporary buff granted by e.g. a guild-level event) verbatim as scalars -- these are real
-- single-value fields in the legacy row, not a packed/array blob, so no normalization is needed.
-- gChangeLeader and gInsertTime are legacy operational bookkeeping (a leader-change-in-progress flag,
-- and a DB row-insert timestamp with a hardcoded '2021-01-01' default that was never meaningfully used)
-- -- CreatedAtUtc replaces gInsertTime with a real default; gChangeLeader is dropped as it described a
-- transient in-flight legacy DB operation that Fenrir's application layer will handle in-process instead.
CREATE TABLE game.Guilds
(
    GuildId           INT IDENTITY(1,1) NOT NULL,
    Name              NVARCHAR(12) NOT NULL,                                          -- legacy gName, MAX_GUILD_NAME_LENGTH=13 (12 chars + NUL)
    Grade             INT    NOT NULL CONSTRAINT DF_Guilds_Grade DEFAULT 0,           -- gGrade
    MasterCharacterId INT NULL,                                                       -- gMaster01, resolved to a real character
    Points            INT    NOT NULL CONSTRAINT DF_Guilds_Points DEFAULT 0,          -- gPoint
    BuffType          INT    NOT NULL CONSTRAINT DF_Guilds_BuffType DEFAULT 0,        -- gBuffType
    BuffState         INT    NOT NULL CONSTRAINT DF_Guilds_BuffState DEFAULT 0,       -- gBuffState
    BuffTime          INT    NOT NULL CONSTRAINT DF_Guilds_BuffTime DEFAULT 0,        -- gBuffTime
    BuffTimeForDiff   BIGINT NOT NULL CONSTRAINT DF_Guilds_BuffTimeForDiff DEFAULT 0, -- gBuffTimeForDiff (bigint(19) in the legacy dump)
    Logo              INT    NOT NULL CONSTRAINT DF_Guilds_Logo DEFAULT 0,            -- gLogo
    CreatedAtUtc      DATETIME2(3) NOT NULL CONSTRAINT DF_Guilds_CreatedAtUtc DEFAULT SYSUTCDATETIME(),
    CONSTRAINT PK_Guilds PRIMARY KEY CLUSTERED (GuildId),
    CONSTRAINT UQ_Guilds_Name UNIQUE (Name),
    CONSTRAINT FK_Guilds_MasterCharacter FOREIGN KEY (MasterCharacterId) REFERENCES game.Characters (CharacterId),
    INDEX             IX_Guilds_MasterCharacter NONCLUSTERED (MasterCharacterId)
);
