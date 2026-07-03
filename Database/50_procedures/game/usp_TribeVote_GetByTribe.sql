-- database/50_procedures/game/usp_TribeVote_GetByTribe.sql
-- Contract: current leadership-election candidate slate for one tribe (see game.TribeVotes for the
-- legacy mTribeVote* field mapping this reconstructs).
-- Params:
--   @TribeId TINYINT -- game.Tribes.TribeId (0-3)
-- Result set (RS0), ordered by VotePoint descending (highest first -- mirrors the legacy tally in
-- ts25center/S04_MyWork02.cpp case 55, which picks the slot with the single highest VotePoint as the
-- next tribe master), then SlotIndex for a stable tie-break order:
--   1. TribeId              TINYINT
--   2. SlotIndex            TINYINT (0-9)
--   3. CandidateCharacterId INT
--   4. CandidateLevel       SMALLINT
--   5. KillOtherTribeCount  INT
--   6. VotePoint            INT
--   7. RegisteredAtUtc      DATETIME2(3)
-- Idempotent: yes (read-only).
-- Errors: none (an unknown @TribeId, or a tribe with no active election, simply returns zero rows).
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
