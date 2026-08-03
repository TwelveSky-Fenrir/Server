CREATE PROCEDURE game.usp_Character_ClampVitalsFloor @CharacterId INT,
                                                     @FlushSequence BIGINT,
                                                     @Life INT,
                                                     @Mana INT
AS
BEGIN
    SET
        NOCOUNT ON;
    SET
        XACT_ABORT ON;

    UPDATE game.Characters
    SET Life          = @Life,
        Mana          = @Mana,
        FlushSequence = @FlushSequence,
        UpdatedAtUtc  = SYSUTCDATETIME()
    WHERE CharacterId = @CharacterId
      AND @FlushSequence > FlushSequence;
END;
