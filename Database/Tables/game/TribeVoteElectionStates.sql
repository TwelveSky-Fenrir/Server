CREATE TABLE game.TribeVoteElectionStates
(
    TribeId      TINYINT          NOT NULL,
    CycleId      UNIQUEIDENTIFIER NULL,
    Phase        TINYINT          NOT NULL
        CONSTRAINT DF_TribeVoteElectionStates_Phase DEFAULT 0,
    UpdatedAtUtc DATETIME2(3)     NOT NULL
        CONSTRAINT DF_TribeVoteElectionStates_UpdatedAtUtc DEFAULT SYSUTCDATETIME(),
    CONSTRAINT PK_TribeVoteElectionStates PRIMARY KEY CLUSTERED (TribeId),
    CONSTRAINT CK_TribeVoteElectionStates_Phase CHECK (Phase BETWEEN 0 AND 4),
    CONSTRAINT FK_TribeVoteElectionStates_Tribe FOREIGN KEY (TribeId) REFERENCES game.Tribes (TribeId)
);
