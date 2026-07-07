-- Corrective fix for game.usp_HeroRanking_AddPoints
-- (StoredProcedures/game/usp_HeroRanking_AddPoints.sql, never edited in place -- DbMigrator journals every
-- script by content hash and refuses a changed re-apply).
--
-- Bug (behavior contract: hero-rank Level column freeze on repeat point grants, area
-- hero-rank-guild-buff-field-audit, severity Minor): legacy's AddOrUpdateHeroRank
-- (Server/ts25center/S08_MyDB.cpp:213-216) is `... ON DUPLICATE KEY UPDATE hTribe=%d,hPoint=hPoint+%d;` --
-- hLevel is passed into the INSERT's VALUES list but is conspicuously absent from the UPDATE clause, so
-- once a hero-rank row exists for a character, its Level column is frozen forever at whatever level the
-- character was when they first ever earned a hero point for that ranking period; only TribeId and Points
-- are kept fresh on every subsequent grant (the calling dispatch case, Server/ts25center/S04_MyWork02.cpp:
-- 1184-1189, discards the routine's return value and does nothing else). This freeze has no bearing on
-- ranking order -- both read paths order strictly by Points descending (Server/ts25center/S08_MyDB.cpp:295,
-- :409) -- it only affects the level value displayed alongside a ranked character. The period reset
-- elsewhere in the same source file (Server/ts25center/S08_MyDB.cpp:440-443) clears the whole current-rank
-- table, so the freeze re-seeds itself from the next grant after every reset.
--
-- This procedure instead did `Level = COALESCE(@Level, Level)` in its UPDATE branch, refreshing Level on
-- every grant whenever a non-null value was supplied -- a confirmed divergence from legacy's
-- freeze-at-first-grant behavior.
--
-- Fix: the UPDATE branch (existing-row path) no longer touches Level at all, matching legacy's omission --
-- Level is only ever written by the INSERT branch (first-ever grant for a given (CharacterId, PeriodKind)
-- row, or the first grant after a period rollover/reset re-creates it). @Level is still accepted as a
-- parameter (still needed for that INSERT) so the procedure signature and the C# call site
-- (HeroRankingRepository.AddPointsAsync) are unchanged.
--
-- Deliberately NOT touched by this fix: the sibling observation noted in the same behavior contract, that
-- this procedure's TribeId refresh is conditional on a non-null @TribeId (COALESCE) whereas legacy
-- overwrites TribeId unconditionally on every call. That was explicitly flagged in the contract as a
-- separate, not-independently-verified, out-of-scope concern -- left for its own citation/decision, not
-- addressed here.
CREATE OR ALTER PROCEDURE game.usp_HeroRanking_AddPoints @CharacterId INT,
    @PeriodKind TINYINT,
    @Delta      INT,
    @TribeId    TINYINT = NULL,
    @Level      INT = NULL
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    UPDATE game.HeroRankings
    SET Points        = Points + @Delta,
        TribeId       = COALESCE(@TribeId, TribeId),
        RecordedAtUtc = SYSUTCDATETIME()
    WHERE CharacterId = @CharacterId
      AND PeriodKind = @PeriodKind;

    IF @@ROWCOUNT = 0
        INSERT INTO game.HeroRankings (CharacterId, PeriodKind, Points, TribeId, Level, RecordedAtUtc)
        VALUES (@CharacterId, @PeriodKind, @Delta, @TribeId, @Level, SYSUTCDATETIME());

    SELECT Points
    FROM game.HeroRankings
    WHERE CharacterId = @CharacterId
      AND PeriodKind = @PeriodKind;
END;
GO
