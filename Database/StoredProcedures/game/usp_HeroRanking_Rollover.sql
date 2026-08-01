-- Weekly Current->Previous rollover, ported from the legacy's MyDB::UpdateRank (S08_MyDB.cpp:348-467):
-- once 7 real days have elapsed since the last flip, the Previous period is replaced by (up to) the top 10
-- Current-period characters per tribe, and Current is cleared for the new period to accumulate into.
-- Points<=0 and TribeId IS NULL rows are dropped rather than promoted, matching the legacy's own
-- "ORDER BY hPoint DESC LIMIT 10" / "if (hPoint < 1) continue" (S08_MyDB.cpp:409,423) -- both HeroRankBuilder
-- and HeroRewardResolver already treat anything past rank 10 or without a live tribe as unreachable, so there
-- is nothing to gain from keeping it around.
-- Safe to call redundantly and concurrently from every GameServer shard: the whole check-and-flip runs
-- inside one transaction holding an update lock on the singleton sentinel row (game.HeroRankingRolloverState),
-- so a second caller that races in just blocks until the first commits, then reads the refreshed date and
-- no-ops. Returns whether THIS call performed the flip, purely so the caller can log it.
CREATE PROCEDURE game.usp_HeroRanking_Rollover
AS
BEGIN
    SET
        NOCOUNT ON;
    SET
        XACT_ABORT ON;

    DECLARE
        @RolledOver BIT = 0;

    BEGIN
        TRANSACTION;

    IF
        NOT EXISTS (SELECT 1 FROM game.HeroRankingRolloverState WITH (UPDLOCK, HOLDLOCK) WHERE Id = 1)
        INSERT INTO game.HeroRankingRolloverState (Id) VALUES (1);

    DECLARE
        @LastRolloverAtUtc DATETIME2(3);
    SELECT @LastRolloverAtUtc = LastRolloverAtUtc
    FROM game.HeroRankingRolloverState
    WITH (UPDLOCK, HOLDLOCK)
    WHERE Id = 1;

    IF
        DATEDIFF(DAY, @LastRolloverAtUtc, SYSUTCDATETIME()) >= 7
        BEGIN
            DELETE
            FROM game.HeroRankings
            WHERE PeriodKind = 1;

            WITH RankedCurrent AS (SELECT CharacterId,
                                          TribeId,
                                          Points,
                                          Level,
                                          ROW_NUMBER() OVER (PARTITION BY TribeId ORDER BY Points DESC) AS Rn
                                   FROM game.HeroRankings
                                   WHERE PeriodKind = 0
                                     AND TribeId IS NOT NULL
                                     AND Points > 0)
            INSERT
            INTO game.HeroRankings (CharacterId, PeriodKind, Points, TribeId, Level, RewardClaimed, Description,
                                    RecordedAtUtc)
            SELECT CharacterId,
                   1,
                   Points,
                   TribeId,
                   Level,
                   0,
                   NULL,
                   SYSUTCDATETIME()
            FROM RankedCurrent
            WHERE Rn <= 10;

            DELETE
            FROM game.HeroRankings
            WHERE PeriodKind = 0;

            UPDATE game.HeroRankingRolloverState
            SET LastRolloverAtUtc = SYSUTCDATETIME()
            WHERE Id = 1;

            SET
                @RolledOver = 1;
        END;

    COMMIT TRANSACTION;

    SELECT @RolledOver AS RolledOver;
END;
