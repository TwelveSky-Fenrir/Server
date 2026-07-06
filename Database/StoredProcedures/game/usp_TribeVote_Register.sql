-- database/50_procedures/game/usp_TribeVote_Register.sql
-- CZ_TRIBE_VOTE_SEND tSort 1, TRIBE_VOTE_V2 branch (S04_MyWork02.cpp:11610+): registers a Force Leader
-- candidate into the caller-chosen slot, displacing whoever is currently there. The application layer has
-- already verified the displacement is legal (challenger's KillOtherTribeCount strictly exceeds the
-- incumbent's, or the slot is empty) and that this character is not already registered in a different
-- slot for this tribe -- this proc only performs the upsert itself.
CREATE PROCEDURE game.usp_TribeVote_Register @TribeId              TINYINT,
    @SlotIndex            TINYINT,
    @CandidateCharacterId INT,
    @CandidateLevel       SMALLINT,
    @KillOtherTribeCount  INT
AS
BEGIN
    SET
NOCOUNT ON;
    SET
XACT_ABORT ON;

    IF
EXISTS (SELECT 1 FROM game.TribeVotes WHERE TribeId = @TribeId AND SlotIndex = @SlotIndex)
UPDATE game.TribeVotes
SET CandidateCharacterId = @CandidateCharacterId,
    CandidateLevel       = @CandidateLevel,
    KillOtherTribeCount  = @KillOtherTribeCount,
    VotePoint            = 0,
    RegisteredAtUtc      = SYSUTCDATETIME()
WHERE TribeId = @TribeId
  AND SlotIndex = @SlotIndex;
ELSE
        INSERT INTO game.TribeVotes (TribeId, SlotIndex, CandidateCharacterId, CandidateLevel, KillOtherTribeCount, VotePoint)
        VALUES (@TribeId, @SlotIndex, @CandidateCharacterId, @CandidateLevel, @KillOtherTribeCount, 0);
END;
