CREATE TABLE game.TribeVoteElectionVoters
(
    CycleId          UNIQUEIDENTIFIER NOT NULL,
    VoterCharacterId INT              NOT NULL,
    TribeId          TINYINT          NOT NULL,
    SlotIndex        TINYINT          NOT NULL,
    VotePoints       INT              NOT NULL,
    VotedAtUtc       DATETIME2(3)     NOT NULL
        CONSTRAINT DF_TribeVoteElectionVoters_VotedAtUtc DEFAULT SYSUTCDATETIME(),
    CONSTRAINT PK_TribeVoteElectionVoters PRIMARY KEY CLUSTERED (CycleId, VoterCharacterId),
    CONSTRAINT CK_TribeVoteElectionVoters_SlotIndex CHECK (SlotIndex BETWEEN 0 AND 9),
    CONSTRAINT CK_TribeVoteElectionVoters_VotePoints CHECK (VotePoints > 0),
    CONSTRAINT FK_TribeVoteElectionVoters_Character FOREIGN KEY (VoterCharacterId)
        REFERENCES game.Characters (CharacterId),
    CONSTRAINT FK_TribeVoteElectionVoters_Candidate FOREIGN KEY (CycleId, TribeId, SlotIndex)
        REFERENCES game.TribeVoteElectionCandidates (CycleId, TribeId, SlotIndex)
);
