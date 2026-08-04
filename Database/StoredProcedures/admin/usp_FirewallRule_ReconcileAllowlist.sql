CREATE PROCEDURE admin.usp_FirewallRule_ReconcileAllowlist @ExpiredGraceSeconds INT = 900,
                                                           @BatchSize INT = 1000,
                                                           @MaxBatches INT = 100
AS
BEGIN
    SET
        NOCOUNT ON;
    SET
        XACT_ABORT ON;
    SET
        DEADLOCK_PRIORITY LOW;

    SET @ExpiredGraceSeconds = CASE WHEN @ExpiredGraceSeconds < 0 THEN 0 ELSE @ExpiredGraceSeconds END;
    SET @BatchSize = CASE WHEN @BatchSize < 1 THEN 1 ELSE @BatchSize END;
    SET @MaxBatches = CASE WHEN @MaxBatches < 1 THEN 1 ELSE @MaxBatches END;

    DECLARE @ExpiredBefore DATETIME2(3) = DATEADD(SECOND, -@ExpiredGraceSeconds, SYSUTCDATETIME());
    DECLARE @Removed INT;
    DECLARE @Batches INT = 0;

    WHILE 1 = 1
        BEGIN
            BEGIN TRY
                DELETE TOP (@BatchSize)
                FROM admin.FirewallRules
                WHERE RuleType NOT IN (1, 3)
                   OR ExpiresAtUtc <= @ExpiredBefore;

                SET @Removed = @@ROWCOUNT;
            END TRY
            BEGIN CATCH
                IF
                    ERROR_NUMBER() NOT IN (1205, 1222)
                    THROW;

                BREAK;
            END CATCH;

            SET @Batches = @Batches + 1;

            IF
                @Removed < @BatchSize OR @Batches >= @MaxBatches
                BREAK;
        END;
END;
