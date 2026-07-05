-- Deliberately does not touch SkillPoints: that counter rides the existing write-behind progression
-- path (usp_Character_PersistProgressBatch) instead of a second synchronous path here.
CREATE PROCEDURE game.usp_CharacterSkills_UpsertSlot @CharacterId INT,
    @SlotIndex   TINYINT,
    @SkillId     INT,
    @Grade       INT
AS
BEGIN
    SET
NOCOUNT ON;
    SET
XACT_ABORT ON;

DELETE
FROM game.CharacterSkills
WHERE CharacterId = @CharacterId
  AND SlotIndex = @SlotIndex;

INSERT INTO game.CharacterSkills (CharacterId, SlotIndex, SkillId, Grade)
VALUES (@CharacterId, @SlotIndex, @SkillId, @Grade);
END;
