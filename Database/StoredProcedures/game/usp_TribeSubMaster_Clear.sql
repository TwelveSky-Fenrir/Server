CREATE PROCEDURE game.usp_TribeSubMaster_Clear @TribeId TINYINT,
                                               @CharacterId INT
AS
BEGIN
    SET
        NOCOUNT ON;
    SET
        XACT_ABORT ON;

    DELETE
    FROM game.TribeSubMasters
    WHERE TribeId = @TribeId
      AND CharacterId = @CharacterId;
END;
