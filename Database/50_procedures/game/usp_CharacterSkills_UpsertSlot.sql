-- database/50_procedures/game/usp_CharacterSkills_UpsertSlot.sql
-- Contract: durably writes ONE learned-skill slot (game.CharacterSkills), D7 regime (b) -- the
-- CharacterSkills twin of usp_CharacterItems_ReplaceContainer: "which skill occupies which slot" is
-- itemized, value-object-like state (exactly like game.CharacterItems -- a row simply not existing IS
-- "slot empty", same normalization rule), so it must be durable BEFORE the client ever sees success,
-- never a lossy write-behind step. Covers BOTH tSort 202/233 (learn a NEW skill into an empty slot) and
-- tSort 203 (upgrade an ALREADY-learned skill's Grade in place) -- both are just "this slot's final
-- content", so one DELETE-then-INSERT (never MERGE, architecture reference §12.3) handles either.
-- Deliberately does NOT touch game.Characters.SkillPoints: unlike Money (which has NO write-behind
-- fallback at all, D7 regime (b) exclusively), SkillPoints already rides the EXISTING eventually-consistent
-- write-behind path (game.tvp_CharacterProgress/usp_Character_PersistProgressBatch, D7 regime (a)) exactly
-- like every other progression counter (Level, StatPoints, Experience) -- see
-- Fenrir.Application.Game.Skills.SkillLearnResolver's own remarks for why keeping SkillPoints on that
-- SAME path (rather than inventing a second, synchronous write path for it here) avoids a real
-- FlushSequence race against the periodic write-behind flusher.
-- Params:
--   @CharacterId INT
--   @SlotIndex   TINYINT -- 0-39 (aSkill[40], CK_CharacterSkills_SlotIndex)
--   @SkillId     INT     -- FK world.Skills
--   @Grade       INT     -- legacy sPoint (invested grade/upgrade value)
-- Result set: none.
-- Idempotent: yes -- replaying the same (@SlotIndex, @SkillId, @Grade) is a no-op in effect.
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
