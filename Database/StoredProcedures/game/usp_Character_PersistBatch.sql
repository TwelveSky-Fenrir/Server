-- Idempotent: a row is only applied when incoming FlushSequence is strictly greater than the
-- stored one, so a duplicate or out-of-order batch delivery is a silent no-op per character.
CREATE PROCEDURE game.usp_Character_PersistBatch @Positions game.tvp_CharacterPosition READONLY
AS
BEGIN
    SET
        NOCOUNT ON;
    SET
        XACT_ABORT ON;

    UPDATE c
    SET c.MapId         = s.MapId,
        c.PosX          = s.PosX,
        c.PosY          = s.PosY,
        c.PosZ          = s.PosZ,
        c.Heading       = s.Heading,
        c.FlushSequence = s.FlushSequence,
        c.UpdatedAtUtc  = SYSUTCDATETIME()
    FROM game.Characters AS c
             JOIN @Positions AS s
                  ON s.CharacterId = c.CharacterId
    WHERE s.FlushSequence > c.FlushSequence; -- idempotence guard
END;
