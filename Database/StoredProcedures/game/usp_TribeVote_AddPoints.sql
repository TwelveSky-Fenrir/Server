CREATE PROCEDURE game.usp_TribeVote_AddPoints @TribeId TINYINT,
                                              @SlotIndex TINYINT,
                                              @Points INT
AS
BEGIN
    SET
        NOCOUNT ON;
    SET
        XACT_ABORT ON;

    UPDATE game.TribeVotes
    SET VotePoint = VotePoint + @Points
    WHERE TribeId = @TribeId
      AND SlotIndex = @SlotIndex;
END;
