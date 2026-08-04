CREATE TABLE game.TribeVoteElectionCandidates
(
    CycleId              UNIQUEIDENTIFIER NOT NULL,
    TribeId              TINYINT          NOT NULL,
    SlotIndex            TINYINT          NOT NULL,
    CandidateCharacterId INT              NOT NULL,
    CandidateLevel       SMALLINT         NOT NULL,
    KillOtherTribeCount  INT              NOT NULL,
    VotePoint            INT              NOT NULL
        CONSTRAINT DF_TribeVoteElectionCandidates_VotePoint DEFAULT 0,
    RegisteredAtUtc      DATETIME2(3)     NOT NULL
        CONSTRAINT DF_TribeVoteElectionCandidates_RegisteredAtUtc DEFAULT SYSUTCDATETIME(),
    CONSTRAINT PK_TribeVoteElectionCandidates PRIMARY KEY CLUSTERED (CycleId, TribeId, SlotIndex),
    CONSTRAINT UQ_TribeVoteElectionCandidates_Candidate UNIQUE (CycleId, TribeId, CandidateCharacterId),
    CONSTRAINT CK_TribeVoteElectionCandidates_SlotIndex CHECK (SlotIndex BETWEEN 0 AND 9),
    CONSTRAINT CK_TribeVoteElectionCandidates_CandidateLevel CHECK (CandidateLevel >= 0),
    CONSTRAINT CK_TribeVoteElectionCandidates_KillOtherTribeCount CHECK (KillOtherTribeCount >= 0),
    CONSTRAINT CK_TribeVoteElectionCandidates_VotePoint CHECK (VotePoint >= 0),
    CONSTRAINT FK_TribeVoteElectionCandidates_Tribe FOREIGN KEY (TribeId) REFERENCES game.Tribes (TribeId),
    CONSTRAINT FK_TribeVoteElectionCandidates_Character FOREIGN KEY (CandidateCharacterId)
        REFERENCES game.Characters (CharacterId)
);
