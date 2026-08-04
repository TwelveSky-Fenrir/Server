CREATE TABLE game.WorldState
(
    Id                   TINYINT      NOT NULL
        CONSTRAINT DF_WorldState_Id DEFAULT 1,
    Zone038WinTribe      TINYINT      NULL,
    Zone038WinTribeTime  INT          NULL,
    TribeSymbolBattle    BIT          NOT NULL
        CONSTRAINT DF_WorldState_TribeSymbolBattle DEFAULT 0,
    MonsterSymbol        TINYINT      NULL,
    MonsterSymbolEndTime INT          NULL,
    HighTribe            TINYINT      NULL,
    UpdateTribePoint     SMALLINT     NOT NULL
        CONSTRAINT DF_WorldState_UpdateTribePoint DEFAULT 0,
    Revision             BIGINT       NOT NULL
        CONSTRAINT DF_WorldState_Revision DEFAULT 0,
    UpdatedAtUtc         DATETIME2(3) NOT NULL
        CONSTRAINT DF_WorldState_UpdatedAtUtc DEFAULT SYSUTCDATETIME(),
    CONSTRAINT PK_WorldState PRIMARY KEY CLUSTERED (Id),
    CONSTRAINT CK_WorldState_Id CHECK (Id = 1),
    CONSTRAINT CK_WorldState_RevisionNonNegative CHECK (Revision >= 0),
    CONSTRAINT FK_WorldState_Zone038WinTribe FOREIGN KEY (Zone038WinTribe) REFERENCES game.Tribes (TribeId),
    CONSTRAINT FK_WorldState_MonsterSymbol FOREIGN KEY (MonsterSymbol) REFERENCES game.Tribes (TribeId),
    CONSTRAINT FK_WorldState_HighTribe FOREIGN KEY (HighTribe) REFERENCES game.Tribes (TribeId)
);
