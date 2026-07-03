-- Minimal M1 columns only -- just enough to drive LC_USER_AVATAR_RECV2 (char-select list) and the
-- ZC_REGISTER_AVATAR_RECV/AVATAR_INFO subset the M1 world-entry path actually reads
-- (M1_Legacy_Wire_Contract.md §4.4/§6.2). NOT a full AVATAR_INFO mirror: M1 is foundation +
-- minimal playable path, not gameplay or data migration (see plan's "hors périmètre M1").
-- Slot is the legacy 3-slot model (MAX_USER_AVATAR_NUM=3): CL_CREATE_AVATAR_SEND2/CL_DELETE_AVATAR_SEND
-- target tAvatarPost directly, so it must be an explicit column, not derived from insertion order.
CREATE TABLE game.Characters
(
    CharacterId   INT IDENTITY(1,1) NOT NULL,
    AccountId     INT      NOT NULL,
    Slot          TINYINT  NOT NULL,
    Name          NVARCHAR(13)  NOT NULL,                                             -- MAX_AVATAR_NAME_LENGTH=13, a real wire truncation limit
    Tribe         TINYINT  NOT NULL,
    Gender        TINYINT  NOT NULL,
    HeadType      TINYINT  NOT NULL,
    FaceType      TINYINT  NOT NULL,
    Level         SMALLINT NOT NULL CONSTRAINT DF_Characters_Level DEFAULT 1,
    MapId         SMALLINT NOT NULL,
    PosX          REAL     NOT NULL,
    PosY          REAL     NOT NULL,
    PosZ          REAL     NOT NULL,
    Heading       REAL     NOT NULL CONSTRAINT DF_Characters_Heading DEFAULT 0,       -- ACTION_INFO.aFront, orientation
    Life          INT      NOT NULL,
    MaxLife       INT      NOT NULL,
    Mana          INT      NOT NULL,
    MaxMana       INT      NOT NULL,
    FlushSequence BIGINT   NOT NULL CONSTRAINT DF_Characters_FlushSequence DEFAULT 0, -- §12.6 idempotent write-behind
    CreatedAtUtc  DATETIME2(3)  NOT NULL CONSTRAINT DF_Characters_CreatedAtUtc DEFAULT SYSUTCDATETIME(),
    UpdatedAtUtc  DATETIME2(3)  NOT NULL CONSTRAINT DF_Characters_UpdatedAtUtc DEFAULT SYSUTCDATETIME(),
    CONSTRAINT PK_Characters PRIMARY KEY CLUSTERED (CharacterId),
    CONSTRAINT UQ_Characters_Name UNIQUE (Name),
    CONSTRAINT UQ_Characters_Account_Slot UNIQUE (AccountId, Slot),
    CONSTRAINT CK_Characters_Slot CHECK (Slot BETWEEN 0 AND 2),
    CONSTRAINT FK_Characters_Account FOREIGN KEY (AccountId) REFERENCES auth.Accounts (AccountId),
    INDEX         IX_Characters_Account NONCLUSTERED (AccountId) INCLUDE (Slot, Name, Tribe, Level)
);
