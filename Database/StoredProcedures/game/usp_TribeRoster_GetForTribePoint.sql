-- database/50_procedures/game/usp_TribeRoster_GetForTribePoint.sql
-- Feeds the level/rebirth-based tribe-point recompute (behavior contract C17, Part C / side effect C1;
-- Server/ts25center/S08_MyDB.cpp:108-135, GetTribePoint under #ifdef OLD_TRIBE_POINT). Returns one row per
-- persisted character that clears the level-145 max-level gate, carrying the four fields the pure
-- TribePointLevelRecompute.ComputeTotals formula needs: Tribe, primary Level (aLevel1), secondary Level2
-- (aLevel2) and RebirthCount (aRebirthNum). The 1000 baseline, the (level-112)+3*Level2+3*RebirthCount sum,
-- and tribe 3's +800 bonus are all applied in the C# domain, not here -- this procedure is purely the roster
-- read the ITribePointRosterGateway seam was created for.
--
-- Deliberately sources from the genuine character/avatar roster (game.Characters = legacy AvatarInfo,
-- mDBTable[2]), NOT the RankInfo/experience table (mDBTable[9]) the legacy's shipped GetTribePoint query
-- mistakenly reads -- that table carries no level/tribe/rebirth columns at all, so the live query almost
-- certainly failed outright in the shipped server. See TribePointLevelRecomputeService's own remarks for the
-- full "known legacy bug, deliberately not reproduced" citation trail.
--
-- The Level >= 145 filter matches the legacy's own "max-level characters only" SQL gate and keeps this a
-- small filtered read rather than a full table scan; the recompute runs every ~6 ticks, so a supporting
-- filtered covering index exists (Migrations/040_characters_maxlevel_tribepoint_index.sql). The C# domain
-- formula re-applies its own < 145 skip defensively, so returning only qualifying rows is safe.
CREATE PROCEDURE game.usp_TribeRoster_GetForTribePoint
AS
BEGIN
    SET
        NOCOUNT ON;

    SELECT c.Tribe                     AS TribeId,
           c.Level                     AS Level1,
           c.Level2,
           CAST(c.RebirthCount AS SMALLINT) AS RebirthCount
    FROM game.Characters c
    WHERE c.Level >= 145;
END;
