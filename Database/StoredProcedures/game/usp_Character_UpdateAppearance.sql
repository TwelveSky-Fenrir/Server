-- Atomically updates a character's HeadType and FaceType (used by the Appearance Change Scroll, item 1214).
-- Gender is not changed by item 1214; see usp_Character_UpdateGender for the Gender Scroll (item 1171).
--
-- Return codes:
--   0   updated successfully
--   1   character not found
CREATE PROCEDURE game.usp_Character_UpdateAppearance @CharacterId INT,
                                                     @HeadType    TINYINT,
                                                     @FaceType    TINYINT
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
