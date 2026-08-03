CREATE PROCEDURE game.usp_Character_UpdateAppearance @CharacterId INT,
                                                     @HeadType TINYINT,
                                                     @FaceType TINYINT
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    UPDATE game.Characters
    SET HeadType     = @HeadType,
        FaceType     = @FaceType,
        UpdatedAtUtc = SYSUTCDATETIME()
    WHERE CharacterId = @CharacterId;

    SELECT CASE WHEN @@ROWCOUNT = 0 THEN 1 ELSE 0 END;
END;
