-- database/50_procedures/game/usp_Character_PersistBatch.sql
-- Contract: write-behind flush of character positions (architecture reference §12.6,
-- flusher contract §10.5) -- one round trip for hundreds of entities.
-- Params:
--   @Positions game.tvp_CharacterPosition READONLY
--     rows: CharacterId INT, FlushSequence BIGINT, MapId SMALLINT, PosX/PosY/PosZ REAL,
--     Heading REAL (mirrors game.Characters column types 1:1).
-- Result set: none.
-- Idempotent: yes -- replaying a batch (network retry) is strictly neutral: a row is only
-- applied when the incoming FlushSequence is strictly greater than the one already stored,
-- so a duplicate or out-of-order delivery is a silent no-op per character.
-- No MERGE (forbidden, §12.3): a single guarded UPDATE ... JOIN is sufficient because this
-- proc only ever updates existing characters (creation goes through usp_Character_Create).
-- Errors: none.
CREATE PROCEDURE game.usp_Character_PersistBatch @Positions game.tvp_CharacterPosition READONLY
AS
BEGIN
    SET
NOCOUNT ON; SET
XACT_ABORT ON;

UPDATE c
SET c.MapId         = s.MapId,
    c.PosX          = s.PosX,
    c.PosY          = s.PosY,
    c.PosZ          = s.PosZ,
    c.Heading       = s.Heading,
    c.FlushSequence = s.FlushSequence,
    c.UpdatedAtUtc  = SYSUTCDATETIME() FROM game.Characters AS c
      JOIN @Positions      AS s
ON s.CharacterId = c.CharacterId
WHERE s.FlushSequence > c.FlushSequence; -- the idempotence guard
END;
