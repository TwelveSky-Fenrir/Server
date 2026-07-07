-- item-usage-consumables (Critical): stat/elixir-potion consumption (StatPotionResolver, wired into
-- UseInventoryItemService.ResolveAsync) increments one of game.Characters.EatLifePotion/EatManaPotion/
-- EatStrPotion/EatDexPotion/EatElePotion per successful use. Those five columns already exist (0-400
-- CHECK-constrained) and are already read back by usp_Character_GetForWorldEntry, but the write-behind
-- persist-back path (game.tvp_CharacterProgress / usp_Character_PersistProgressBatch) never carried them,
-- so a stat-potion counter incremented in PlayerRuntimeState would silently revert to its last-persisted
-- value on the next flush cycle (or be lost outright on a non-graceful process kill).
--
-- Per the "never edit an already-applied script" rule, this is a new migration rather than an edit of
-- Schemas/Types/game/tvp_CharacterProgress.sql / StoredProcedures/game/usp_Character_PersistProgressBatch.sql
-- in place. SQL Server has no ALTER TYPE for table types, so the procedure (which parameterizes on the
-- type) must be dropped before the type, and both recreated after -- same drop-then-recreate shape as
-- Migrations/010's SessionTickets fix, batch-split on the standalone GO lines below.

DROP PROCEDURE game.usp_Character_PersistProgressBatch;
GO

DROP TYPE game.tvp_CharacterProgress;
GO

CREATE TYPE game.tvp_CharacterProgress AS TABLE
(
    CharacterId        INT      NOT NULL,
    FlushSequence      BIGINT   NOT NULL,
    Level              SMALLINT NOT NULL,
    Level2             SMALLINT NOT NULL,
    Experience         BIGINT   NOT NULL,
    Life               INT      NOT NULL,
    MaxLife            INT      NOT NULL,
    Mana               INT      NOT NULL,
    MaxMana            INT      NOT NULL,
    StatVit            INT      NOT NULL,
    StatStr            INT      NOT NULL,
    StatInt            INT      NOT NULL,
    StatDex            INT      NOT NULL,
    StatPoints         INT      NOT NULL,
    SkillPoints        INT      NOT NULL,
    ContributionPoints INT      NOT NULL,
    Exp2               INT      NOT NULL,
    RebirthCount       INT      NOT NULL,
    EatLifePotion      INT      NOT NULL,
    EatManaPotion      INT      NOT NULL,
    EatStrPotion       INT      NOT NULL,
    EatDexPotion       INT      NOT NULL,
    EatElePotion       INT      NOT NULL
);
GO

-- Idempotent: same strictly-greater FlushSequence guard as usp_Character_PersistBatch (one shared
-- monotonic sequence per character across both flush flavors).
-- Money/BigMoney intentionally absent: balances only move through usp_Character_AdjustMoney, to
-- avoid a last-write-wins flush clobbering a balance.
CREATE PROCEDURE game.usp_Character_PersistProgressBatch @Progress game.tvp_CharacterProgress READONLY
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

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
        c.Exp2               = s.Exp2,
        c.RebirthCount       = s.RebirthCount,
        c.EatLifePotion      = s.EatLifePotion,
        c.EatManaPotion      = s.EatManaPotion,
        c.EatStrPotion       = s.EatStrPotion,
        c.EatDexPotion       = s.EatDexPotion,
        c.EatElePotion       = s.EatElePotion,
        c.FlushSequence      = s.FlushSequence,
        c.UpdatedAtUtc       = SYSUTCDATETIME()
    FROM game.Characters AS c
             JOIN @Progress AS s ON s.CharacterId = c.CharacterId
    WHERE s.FlushSequence > c.FlushSequence; -- idempotence guard
END;
GO
