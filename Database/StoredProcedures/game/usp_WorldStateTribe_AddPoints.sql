CREATE PROCEDURE game.usp_WorldStateTribe_AddPoints @TribeId TINYINT,
                                                    @Delta INT
AS
BEGIN
    SET
        NOCOUNT ON;
    SET
        XACT_ABORT ON;

    UPDATE game.WorldStateTribes
    SET Points = Points + @Delta
    WHERE TribeId = @TribeId;
END;
