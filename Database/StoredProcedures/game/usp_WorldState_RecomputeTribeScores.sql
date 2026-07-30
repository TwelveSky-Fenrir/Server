-- CenterServer world-state authority: the server-side per-tribe stat aggregate feeding
-- ICenterTribeScoreSource / TribeScoreRecompute (Fenrir.Cluster.WorldState) on the 6-beat tribe-score
-- recompute. Behavior contract C17 Part C (Server/ts25center/S08_MyDB.cpp:108-135, GetTribePoint under
-- #ifdef OLD_TRIBE_POINT) -- the same contract usp_TribeRoster_GetForTribePoint serves, but that sibling
-- returns one row per level-145+ character for a shard-side sum; this proc does the GROUP BY / SUM in SQL and
-- returns one row per tribe, because the Center authority wants the finished per-tribe aggregate, not the
-- roster.
--
-- CORRECTS legacy Bug 3: the shipped GetTribePoint aggregated against the RankInfo/experience slot
-- (mDBTable[9]), which carries no level/tribe/rebirth columns -- the live query almost certainly failed
-- outright. The correct source is the genuine avatar roster (game.Characters = legacy AvatarInfo, mDBTable[2]).
--
-- Per-tribe sum, over characters at the level-145 max-level gate, of (level1 - 112) + (level2 * 3) +
-- (rebirthCount * 3), mapping level1/level2/rebirthCount to game.Characters.Level/Level2/RebirthCount. The 1000
-- baseline and tribe 3's flat +800 are applied Fenrir-side by TribeScoreRecompute, NOT here -- this proc
-- returns only the raw stat sums. Each row's contribution is CAST to BIGINT before SUM so the aggregate never
-- overflows INT and matches CenterTribeStatAggregate.StatSum (long).
--
-- The WHERE Level >= 145 gate is served without a table scan by the filtered covering index
-- IX_Characters_MaxLevel_TribePoint (Tribe, Level) INCLUDE (Level2, RebirthCount) WHERE (Level >= 145)
-- (Tables/game/Characters.sql / Migrations/040_characters_maxlevel_tribepoint_index.sql) -- the same index
-- usp_TribeRoster_GetForTribePoint relies on; no new index is needed. Tribes with no qualifying character are
-- simply absent from the result: TribeScoreRecompute defaults every tribe to the 1000 baseline before applying
-- the aggregates it does receive, so an omitted tribe is correct, not a gap.
CREATE PROCEDURE game.usp_WorldState_RecomputeTribeScores
AS
BEGIN
    SET
        NOCOUNT ON;

    SELECT c.Tribe                                                                      AS TribeId,
           SUM(CAST((c.Level - 112) + (c.Level2 * 3) + (c.RebirthCount * 3) AS BIGINT)) AS StatSum
    FROM game.Characters c
    WHERE c.Level >= 145
    GROUP BY c.Tribe;
END;
