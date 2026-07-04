-- database/50_procedures/game/usp_TribeVote_GetByTribe.sql
-- Ordered by VotePoint DESC then SlotIndex, mirroring the legacy tribe-master tally in
-- ts25center/S04_MyWork02.cpp case 55.
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
