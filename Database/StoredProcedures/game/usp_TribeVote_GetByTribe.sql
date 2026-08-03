CREATE PROCEDURE game.usp_TribeVote_GetByTribe @TribeId TINYINT
AS
BEGIN
    SET
        NOCOUNT ON;

    SELECT TribeId,
           SlotIndex,
           CandidateCharacterId,
           CandidateLevel,
           KillOtherTribeCount,
           VotePoint,
           RegisteredAtUtc
    FROM game.TribeVotes
    WHERE TribeId = @TribeId
    ORDER BY VotePoint DESC, SlotIndex;
END;
