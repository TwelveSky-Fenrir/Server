-- See game.Characters.PetGrowth/PetActivity for the per-character-not-per-item model these represent.
CREATE PROCEDURE game.usp_Character_SetPetGrowth @CharacterId INT,
                                                 @PetGrowth INT,
                                                 @PetActivity TINYINT
AS
BEGIN
    SET
        NOCOUNT ON;
    SET
        XACT_ABORT ON;

    UPDATE game.Characters
    SET PetGrowth    = @PetGrowth,
        PetActivity  = @PetActivity,
        UpdatedAtUtc = SYSUTCDATETIME()
    WHERE CharacterId = @CharacterId;
END;
