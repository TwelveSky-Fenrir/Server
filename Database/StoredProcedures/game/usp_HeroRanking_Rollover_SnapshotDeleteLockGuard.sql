-- Additive script: usp_HeroRanking_Rollover.sql stays unchanged (DbMigrator journals it by SHA-256 and would
-- refuse to reapply it if edited). CREATE OR ALTER on the same procedure name, same pattern as
-- usp_Cash_Credit_UpperBoundGuard.sql / usp_Character_Create_ConcurrentCreateGuard.sql.
--
-- Concurrency fix: the original body's own transaction only ever takes UPDLOCK/HOLDLOCK on the singleton
-- game.HeroRankingRolloverState sentinel row, which serializes redundant rollover calls against each other but
-- does nothing to protect game.HeroRankings itself. Its snapshot-then-delete (CTE SELECT into PeriodKind=1,
-- then an unconditional "DELETE ... WHERE PeriodKind = 0") is not otherwise locked, so under RCSI a concurrent
-- usp_HeroRanking_AddPoints call landing between the snapshot read and the DELETE is silently lost: it commits
-- its UPDATE/INSERT to a PeriodKind=0 row the snapshot already passed over, and the DELETE then removes that
-- row without it ever having been promoted into the new Previous period.
-- Fixed by taking UPDLOCK, HOLDLOCK on every PeriodKind=0 row up front, before the snapshot -- the exact same
-- row set the later DELETE unconditionally touches. usp_HeroRanking_AddPoints takes UPDLOCK, HOLDLOCK on its
-- own (CharacterId, PeriodKind) row as its first statement (see that procedure's own header), so a concurrent
-- AddPoints call for any row in that set now blocks until this transaction commits or rolls back, and this
-- transaction's own gate blocks until any AddPoints call already in flight completes -- whichever gets there
-- first is fully applied before the other proceeds, closing the drop window.
CREATE OR ALTER PROCEDURE game.usp_HeroRanking_Rollover
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
            DECLARE
                @LockGate INT;
            SELECT @LockGate = COUNT(*)
            FROM game.HeroRankings WITH (UPDLOCK, HOLDLOCK)
            WHERE PeriodKind = 0;

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
