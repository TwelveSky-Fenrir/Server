-- Contract: no parameters -> RS0, one row per world.Levels (all 145), ordered by Level ascending.
-- Called once at GameServer boot to populate an in-memory cache (FrozenDictionary keyed by Level) --
-- world.* is read-mostly reference data, never queried per-game-tick, so a single flat SELECT is
-- sufficient (no native compilation, no hot-path shape).
-- Idempotent: yes (read-only). Errors: none.
CREATE PROCEDURE world.usp_Level_GetAll
    AS
BEGIN
    SET
NOCOUNT ON;

SELECT Level,
       ExpRangeMin,
       ExpRangeMax,
       RangeInfo3,
       AttackPower,
       DefensePower,
       AttackSuccess,
       AttackBlock,
       ElementAttack,
       Life,
       Mana
FROM world.Levels
ORDER BY Level;
END;
