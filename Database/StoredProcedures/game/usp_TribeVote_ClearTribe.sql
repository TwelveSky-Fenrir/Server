CREATE PROCEDURE game.usp_TribeVote_ClearTribe @TribeId TINYINT
AS
BEGIN
    SET
        NOCOUNT ON;
    SET
        XACT_ABORT ON;

    DELETE
    FROM game.TribeVotes
    WHERE TribeId = @TribeId;
END;
