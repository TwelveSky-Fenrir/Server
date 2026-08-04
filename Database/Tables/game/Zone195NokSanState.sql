CREATE TABLE game.Zone195NokSanStates
(
    StateId      TINYINT      NOT NULL
        CONSTRAINT PK_Zone195NokSanStates PRIMARY KEY
        CONSTRAINT CK_Zone195NokSanStates_StateId CHECK (StateId = 1),
    Revision     BIGINT       NOT NULL
        CONSTRAINT CK_Zone195NokSanStates_Revision CHECK (Revision >= 0),
    OwnerSlot0   TINYINT      NOT NULL
        CONSTRAINT CK_Zone195NokSanStates_OwnerSlot0 CHECK (OwnerSlot0 BETWEEN 0 AND 4),
    OwnerSlot2   TINYINT      NOT NULL
        CONSTRAINT CK_Zone195NokSanStates_OwnerSlot2 CHECK (OwnerSlot2 BETWEEN 0 AND 4),
    OwnerSlot3   TINYINT      NOT NULL
        CONSTRAINT CK_Zone195NokSanStates_OwnerSlot3 CHECK (OwnerSlot3 BETWEEN 0 AND 4),
    StonesHeld0  TINYINT      NOT NULL
        CONSTRAINT CK_Zone195NokSanStates_StonesHeld0 CHECK (StonesHeld0 BETWEEN 0 AND 4),
    StonesHeld1  TINYINT      NOT NULL
        CONSTRAINT CK_Zone195NokSanStates_StonesHeld1 CHECK (StonesHeld1 BETWEEN 0 AND 4),
    StonesHeld2  TINYINT      NOT NULL
        CONSTRAINT CK_Zone195NokSanStates_StonesHeld2 CHECK (StonesHeld2 BETWEEN 0 AND 4),
    StonesHeld3  TINYINT      NOT NULL
        CONSTRAINT CK_Zone195NokSanStates_StonesHeld3 CHECK (StonesHeld3 BETWEEN 0 AND 4),
    UpdatedAtUtc DATETIME2(3) NOT NULL
        CONSTRAINT DF_Zone195NokSanStates_UpdatedAtUtc DEFAULT SYSUTCDATETIME()
);

CREATE TABLE game.Zone195NokSanCaptures
(
    MapId                  SMALLINT      NOT NULL
        CONSTRAINT PK_Zone195NokSanCaptures PRIMARY KEY
        CONSTRAINT CK_Zone195NokSanCaptures_MapId CHECK (MapId IN (99, 100, 196)),
    StateId                TINYINT       NOT NULL
        CONSTRAINT DF_Zone195NokSanCaptures_StateId DEFAULT 1
        CONSTRAINT FK_Zone195NokSanCaptures_StateId REFERENCES game.Zone195NokSanStates (StateId)
        CONSTRAINT CK_Zone195NokSanCaptures_StateId CHECK (StateId = 1),
    Phase                  TINYINT       NOT NULL
        CONSTRAINT CK_Zone195NokSanCaptures_Phase CHECK (Phase BETWEEN 0 AND 2),
    CapturerCharacterId    INT           NOT NULL
        CONSTRAINT CK_Zone195NokSanCaptures_CapturerCharacterId CHECK (CapturerCharacterId >= -1),
    CapturerTribe          TINYINT       NOT NULL
        CONSTRAINT CK_Zone195NokSanCaptures_CapturerTribe CHECK (CapturerTribe BETWEEN 0 AND 3),
    CapturerName           NVARCHAR(13)  NOT NULL,
    RemainingTime          INT           NOT NULL
        CONSTRAINT CK_Zone195NokSanCaptures_RemainingTime CHECK (RemainingTime >= 0),
    PhaseAccumulatorTicks  INT           NOT NULL
        CONSTRAINT CK_Zone195NokSanCaptures_PhaseAccumulatorTicks CHECK (PhaseAccumulatorTicks >= 0),
    UpdatedAtUtc           DATETIME2(3)  NOT NULL
        CONSTRAINT DF_Zone195NokSanCaptures_UpdatedAtUtc DEFAULT SYSUTCDATETIME(),
    CONSTRAINT CK_Zone195NokSanCaptures_IdleShape CHECK
    (
        (Phase = 0 AND CapturerCharacterId = -1 AND CapturerTribe = 0 AND CapturerName = N'' AND
         RemainingTime = 0 AND PhaseAccumulatorTicks = 0)
        OR
        (Phase IN (1, 2) AND CapturerCharacterId > 0 AND CapturerName <> N'')
    )
);
