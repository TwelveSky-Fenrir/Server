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
