-- database/50_procedures/game/usp_TribeVote_ClearTribe.sql
-- TRIBE_WORK tSort 52 (open candidacy)/56 (reset): wipes every registered candidate slot for one tribe,
-- so the next election cycle starts from an empty slate (S04_MyWork02.cpp cases 52/56).
CREATE PROCEDURE game.usp_TribeVote_ClearTribe
    @TribeId TINYINT
AS
BEGIN
    SET NOCOUNT ON;

    DELETE FROM game.TribeVotes
    WHERE TribeId = @TribeId;
END;
