-- Atomically updates a character's Gender, HeadType and FaceType (used by the Gender Scroll, item 1171).
--
-- Return codes:
--   0   updated successfully
--   1   character not found
CREATE PROCEDURE game.usp_Character_UpdateGenderAndAppearance @CharacterId INT,
                                                              @Gender      TINYINT,
                                                              @HeadType    TINYINT,
                                                              @FaceType    TINYINT
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    UPDATE game.Characters
    SET Gender       = @Gender,
        HeadType     = @HeadType,
        FaceType     = @FaceType,
        UpdatedAtUtc = SYSUTCDATETIME()
    WHERE CharacterId = @CharacterId;

    SELECT CASE WHEN @@ROWCOUNT = 0 THEN 1 ELSE 0 END;
END;
