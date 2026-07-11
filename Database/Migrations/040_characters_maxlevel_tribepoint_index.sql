-- Migrations/040_characters_maxlevel_tribepoint_index.sql
-- Supporting index for game.usp_TribeRoster_GetForTribePoint (the level/rebirth-based tribe-point recompute
-- roster read, C17 Part C). That procedure runs every ~6 center-equivalent ticks (roughly every 6 seconds)
-- and scans game.Characters for the max-level subset (Level >= 145). Without an index that is a repeated
-- clustered-index scan of the entire character table on a short cadence.
--
-- A FILTERED COVERING index on exactly that subset turns the recompute into a small covering seek: the
-- WHERE Level >= 145 filter keeps the index to only max-level characters (a tiny fraction of the table), and
-- INCLUDE (Level2, RebirthCount) makes it cover every column the procedure projects (Tribe, Level, Level2,
-- RebirthCount) with no key lookup back to the base table.
--
-- Write-cost justification (per the filtered/covering indexing playbook): the index is maintained ONLY for
-- rows where Level >= 145, and only when one of (Tribe, Level, Level2, RebirthCount) changes on such a row.
-- Those are rare, discrete progression/rebirth/tribe-conversion events for characters already at the level
-- cap -- not part of the per-tick position/progress write-behind hot path (which does not touch these
-- columns for a maxed character). So the maintenance cost is negligible against the read frequency this
-- index serves. Kept as a standalone additive migration (never edit game.Characters' already-applied
-- creation script per the DbMigrator SHA-256 journal rule).
CREATE NONCLUSTERED INDEX IX_Characters_MaxLevel_TribePoint
    ON game.Characters (Tribe, Level)
    INCLUDE (Level2, RebirthCount)
    WHERE Level >= 145;
