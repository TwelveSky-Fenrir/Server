-- Legacy: WORLD_INFO's mTribeSymbol[4]/mTribePoint[4]/mTribeCloseInfo[2], normalized to one row per tribe.
-- HasSymbol simplifies the legacy "which tribe holds it" int to a bool; IsClosed widens the legacy
-- 2-element array to all 4 tribes (tribes 2/3 have no legacy source, default false).
-- Points is the source of truth for tribe points (not game.Tribes); game.usp_Tribe_GetAll LEFT JOINs it in.
-- The 4 rows (TribeId 0-3) are created by game.usp_WorldState_EnsureInitialized, not seeded here.
CREATE TABLE game.WorldStateTribes
(
    TribeId       TINYINT      NOT NULL,
    SymbolDateUtc DATETIME2(3) NULL,
    HasSymbol     BIT          NOT NULL
        CONSTRAINT DF_WorldStateTribes_HasSymbol DEFAULT 1,
    Points        INT          NOT NULL
        CONSTRAINT DF_WorldStateTribes_Points DEFAULT 0,
    IsClosed      BIT          NOT NULL
        CONSTRAINT DF_WorldStateTribes_IsClosed DEFAULT 0,
    CONSTRAINT PK_WorldStateTribes PRIMARY KEY CLUSTERED (TribeId),
    CONSTRAINT FK_WorldStateTribes_Tribe FOREIGN KEY (TribeId) REFERENCES game.Tribes (TribeId)
);
