-- Legacy: mTribeVoteName/Level/KillOtherTribe/Point[tribe][slot] (MAX_TRIBE_VOTE_AVATAR_NUM=10); a row
-- exists only for an occupied slot (Level>0 in the legacy code).
CREATE TABLE game.TribeVotes
(
    TribeId              TINYINT  NOT NULL,
    SlotIndex            TINYINT  NOT NULL,
    CandidateCharacterId INT      NOT NULL,
    CandidateLevel       SMALLINT NOT NULL,
    KillOtherTribeCount  INT      NOT NULL CONSTRAINT DF_TribeVotes_KillOtherTribeCount DEFAULT 0,
    VotePoint            INT      NOT NULL CONSTRAINT DF_TribeVotes_VotePoint DEFAULT 0,
    RegisteredAtUtc      DATETIME2(3) NOT NULL CONSTRAINT DF_TribeVotes_RegisteredAtUtc DEFAULT SYSUTCDATETIME(),
    CONSTRAINT PK_TribeVotes PRIMARY KEY CLUSTERED (TribeId, SlotIndex),
    CONSTRAINT UQ_TribeVotes_Tribe_Candidate UNIQUE (TribeId, CandidateCharacterId),
    CONSTRAINT CK_TribeVotes_SlotIndex CHECK (SlotIndex BETWEEN 0 AND 9),
    CONSTRAINT FK_TribeVotes_Tribe FOREIGN KEY (TribeId) REFERENCES game.Tribes (TribeId),
    CONSTRAINT FK_TribeVotes_CandidateCharacter FOREIGN KEY (CandidateCharacterId) REFERENCES game.Characters (CharacterId)
);
