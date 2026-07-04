-- database/50_procedures/game/usp_Character_SetPetGrowth.sql
-- Contract: persists the active pet's growth counter and activity level (see game.Characters.PetGrowth/
-- PetActivity's own ALTER-script header comment for the per-character-not-per-item simplification this
-- models). Written whenever a pet feeds/grows, its activity decays, or a newly-equipped pet resets both.
-- Params: @CharacterId INT, @PetGrowth INT (>=0), @PetActivity TINYINT (0-100).
-- Result set: none. Idempotent: yes.
CREATE PROCEDURE game.usp_Character_SetPetGrowth
    @CharacterId INT,
    @PetGrowth   INT,
    @PetActivity TINYINT
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    UPDATE game.Characters
    SET PetGrowth   = @PetGrowth,
        PetActivity = @PetActivity,
        UpdatedAtUtc = SYSUTCDATETIME()
    WHERE CharacterId = @CharacterId;
END;
