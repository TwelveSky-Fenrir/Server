-- database/50_procedures/game/usp_TribeVote_AddPoints.sql
-- CZ_TRIBE_VOTE_SEND tSort 3 (vote): adds one voter's computed VotePoint onto their chosen candidate
-- slot. A no-op if the slot is empty -- the application layer has already verified occupancy before
-- calling this (mirrors the legacy's own unconditional `+=`, S04_MyWork02.cpp case 59).
CREATE PROCEDURE game.usp_TribeVote_AddPoints @TribeId   TINYINT,
    @SlotIndex TINYINT,
    @Points    INT
AS
BEGIN
    SET
NOCOUNT ON;

UPDATE game.TribeVotes
SET VotePoint = VotePoint + @Points
WHERE TribeId = @TribeId
  AND SlotIndex = @SlotIndex;
END;
