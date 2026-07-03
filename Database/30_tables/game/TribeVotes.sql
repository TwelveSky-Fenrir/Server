-- INTERPRETATION NOTE: the task brief flagged tribeinfo.mTribeVoteInfo as a "packed/opaque" blob needing
-- a reasonable-effort reconstruction. Direct inspection shows it is NOT actually opaque -- it is a
-- fixed-width serialization of Protocol/STRUCT.h's TRIBE_INFO parallel arrays (confirmed byte-for-byte
-- against CSQLWorldTribe.cpp GetTribe's own pack/unpack code), so this table mirrors that real shape
-- rather than inventing a generic "VoteCount + window" design:
--   mTribeVoteName[MAX_TRIBE_NUM][MAX_TRIBE_VOTE_AVATAR_NUM]              -> CandidateCharacterId (FK, not the legacy name)
--   mTribeVoteLevel[tribe][slot]      -> CandidateLevel   (candidate's level snapshotted at registration, ts25center/S04_MyWork02.cpp case 57)
--   mTribeVoteKillOtherTribe[tribe][slot] -> KillOtherTribeCount (inter-tribe PVP kills accrued during the registration/campaign window, same case 57/case 59 handlers)
--   mTribeVotePoint[tribe][slot]      -> VotePoint (accumulated vote score; case 59 adds to it per vote cast; case 55's tally picks the slot with the highest VotePoint as the new mTribeMaster)
-- MAX_TRIBE_VOTE_AVATAR_NUM=10 (Protocol/DEFINE.h) candidate slots per tribe, chosen by the client at
-- registration time (case 57's tIndexForTribeVote) -- SlotIndex is that raw legacy slot number, kept as
-- a real column (rather than re-deriving ranking from VotePoint) because the legacy tally additionally
-- reads mTribeVoteLevel[index] > 0 as "is this slot occupied at all", independent of point count.
-- A row only exists for an occupied slot (Level>0 in the legacy code) -- normalize, don't transliterate
-- the empty '@'-filled slots.
-- No separate election-window start/end columns: the legacy phase timing (registration open/closed/
-- tallied) lives in the separate WORLD_INFO.mTribeVoteState[MAX_TRIBE_NUM] state machine
-- (ts25center/S04_MyWork02.cpp cases 52-56), which is world/global server state outside this table's
-- scope, not a per-candidate fact -- RegisteredAtUtc is added instead purely for this table's own
-- auditability (when Fenrir's application layer wrote this candidate row), matching this repo's
-- SYSUTCDATETIME() convention.
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
