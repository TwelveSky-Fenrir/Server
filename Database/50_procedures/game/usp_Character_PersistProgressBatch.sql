-- database/50_procedures/game/usp_Character_PersistProgressBatch.sql
-- Contract: write-behind flush of character progression (D7 regime (a): xp/level/vitals/points on the
-- 5 s DirtyTracker cadence) -- one round trip for hundreds of entities, the progression twin of
-- game.usp_Character_PersistBatch.
-- Params:
--   @Progress game.tvp_CharacterProgress READONLY
--     rows: CharacterId, FlushSequence, Level, Level2, Experience, Life/MaxLife/Mana/MaxMana,
--     StatVit/Str/Int/Dex, StatPoints, SkillPoints, ContributionPoints (mirrors game.Characters 1:1).
-- Result set: none.
-- Idempotent: yes -- same strictly-greater FlushSequence guard as usp_Character_PersistBatch: the
-- write-behind host mints ONE monotonic sequence per character shared by the position and progression
-- flavors, so a duplicate or out-of-order delivery of either batch is a silent per-character no-op.
-- Money/BigMoney are deliberately ABSENT: money only moves through the transactional
-- usp_Character_AdjustMoney (D7 regime (b)) -- letting a last-write-wins flush touch a balance would
-- reopen the value-loss window this split exists to close.
-- No MERGE (forbidden, §12.3): a single guarded UPDATE ... JOIN, creation goes through
-- usp_Character_Create.
-- Errors: none.
CREATE PROCEDURE game.usp_Character_PersistProgressBatch @Progress game.tvp_CharacterProgress READONLY
AS
BEGIN
    SET
NOCOUNT ON; SET
XACT_ABORT ON;

UPDATE c
SET c.Level              = s.Level,
    c.Level2             = s.Level2,
    c.Experience         = s.Experience,
    c.Life               = s.Life,
    c.MaxLife            = s.MaxLife,
    c.Mana               = s.Mana,
    c.MaxMana            = s.MaxMana,
    c.StatVit            = s.StatVit,
    c.StatStr            = s.StatStr,
    c.StatInt            = s.StatInt,
    c.StatDex            = s.StatDex,
    c.StatPoints         = s.StatPoints,
    c.SkillPoints        = s.SkillPoints,
    c.ContributionPoints = s.ContributionPoints,
    c.FlushSequence      = s.FlushSequence,
    c.UpdatedAtUtc       = SYSUTCDATETIME() FROM game.Characters AS c
      JOIN @Progress       AS s
ON s.CharacterId = c.CharacterId
WHERE s.FlushSequence > c.FlushSequence; -- the idempotence guard
END;
