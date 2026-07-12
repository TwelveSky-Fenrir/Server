-- Single-character, single-statement counterpart to usp_Character_PersistProgressBatch +
-- usp_Character_PersistBatch, used only by the disconnect-time immediate flush
-- (PositionWriteBehindHost.FlushCharacterNowAsync). Progress and position columns are written in the
-- same UPDATE so the two halves of one logout snapshot can never be split by a mid-sequence failure --
-- unlike the periodic write-behind cycle, this path has no "next cycle" to catch up a dropped half.
-- Same strictly-greater FlushSequence idempotence guard as both batch procedures; both TVPs carry one
-- row each, stamped with the same FlushSequence value by the caller.
CREATE PROCEDURE game.usp_Character_PersistFinalFlush @Progress game.tvp_CharacterProgress READONLY,
                                                       @Position game.tvp_CharacterPosition READONLY
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    UPDATE c
    SET c.Level              = p.Level,
        c.Level2             = p.Level2,
        c.Experience         = p.Experience,
        c.Life               = p.Life,
        c.MaxLife            = p.MaxLife,
        c.Mana               = p.Mana,
        c.MaxMana            = p.MaxMana,
        c.StatVit            = p.StatVit,
        c.StatStr            = p.StatStr,
        c.StatInt            = p.StatInt,
        c.StatDex            = p.StatDex,
        c.StatPoints         = p.StatPoints,
        c.SkillPoints        = p.SkillPoints,
        c.ContributionPoints = p.ContributionPoints,
        c.Exp2               = p.Exp2,
        c.RebirthCount       = p.RebirthCount,
        c.EatLifePotion      = p.EatLifePotion,
        c.EatManaPotion      = p.EatManaPotion,
        c.EatStrPotion       = p.EatStrPotion,
        c.EatDexPotion       = p.EatDexPotion,
        c.EatElePotion       = p.EatElePotion,
        c.DropItemTime       = p.DropItemTime,
        c.M15PetLuckyBoxPity = p.M15PetLuckyBoxPity,
        c.MapId              = q.MapId,
        c.PosX               = q.PosX,
        c.PosY               = q.PosY,
        c.PosZ               = q.PosZ,
        c.Heading            = q.Heading,
        c.FlushSequence      = q.FlushSequence,
        c.UpdatedAtUtc       = SYSUTCDATETIME()
    FROM game.Characters AS c
             JOIN @Progress AS p ON p.CharacterId = c.CharacterId
             JOIN @Position AS q ON q.CharacterId = c.CharacterId
    WHERE q.FlushSequence > c.FlushSequence; -- idempotence guard
END;
