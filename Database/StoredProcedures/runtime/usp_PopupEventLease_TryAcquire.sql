CREATE OR ALTER PROCEDURE runtime.usp_PopupEventLease_TryAcquire @OccurrenceKey VARCHAR(96),
                                                                 @LeaseOwnerId UNIQUEIDENTIFIER,
                                                                 @LeaseDurationSeconds SMALLINT
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    IF NULLIF(LTRIM(RTRIM(@OccurrenceKey)), '') IS NULL
        THROW 50701, N'A popup-event lease occurrence key is required.', 1;

    IF @LeaseOwnerId IS NULL
        THROW 50703, N'A popup-event lease owner is required.', 1;

    IF @LeaseDurationSeconds NOT BETWEEN 1 AND 120
        THROW 50702, N'A popup-event lease duration must be between one and 120 seconds.', 1;

    DECLARE @NowUtc DATETIME2(3) = SYSUTCDATETIME();
    DECLARE @NewLeaseExpiresAtUtc DATETIME2(3) = DATEADD(SECOND, @LeaseDurationSeconds, @NowUtc);
    DECLARE @ExistingLeaseOwnerId UNIQUEIDENTIFIER;
    DECLARE @ExistingLeaseExpiresAtUtc DATETIME2(3);
    DECLARE @Acquired BIT = 0;
    DECLARE @LeaseExpiresAtUtc DATETIME2(3);

    BEGIN TRANSACTION;

    SELECT @ExistingLeaseOwnerId = LeaseOwnerId,
           @ExistingLeaseExpiresAtUtc = LeaseExpiresAtUtc
    FROM runtime.PopupEventLease
    WITH (UPDLOCK, HOLDLOCK)
    WHERE OccurrenceKey = @OccurrenceKey;

    IF @ExistingLeaseOwnerId IS NULL
        BEGIN
            INSERT INTO runtime.PopupEventLease
            (OccurrenceKey, LeaseOwnerId, LeaseExpiresAtUtc, AcquiredAtUtc, RenewedAtUtc)
            VALUES (@OccurrenceKey, @LeaseOwnerId, @NewLeaseExpiresAtUtc, @NowUtc, @NowUtc);

            SET @Acquired = 1;
            SET @LeaseExpiresAtUtc = @NewLeaseExpiresAtUtc;
        END
    ELSE
        IF @ExistingLeaseOwnerId = @LeaseOwnerId
            BEGIN
                UPDATE runtime.PopupEventLease
                SET LeaseExpiresAtUtc = @NewLeaseExpiresAtUtc,
                    RenewedAtUtc      = @NowUtc
                WHERE OccurrenceKey = @OccurrenceKey;

                SET @LeaseExpiresAtUtc = @NewLeaseExpiresAtUtc;
            END
        ELSE
            IF @ExistingLeaseExpiresAtUtc <= @NowUtc
                BEGIN
                    UPDATE runtime.PopupEventLease
                    SET LeaseOwnerId      = @LeaseOwnerId,
                        LeaseExpiresAtUtc = @NewLeaseExpiresAtUtc,
                        AcquiredAtUtc     = @NowUtc,
                        RenewedAtUtc      = @NowUtc
                    WHERE OccurrenceKey = @OccurrenceKey;

                    SET @Acquired = 1;
                    SET @LeaseExpiresAtUtc = @NewLeaseExpiresAtUtc;
                END
            ELSE
                SET @LeaseExpiresAtUtc = @ExistingLeaseExpiresAtUtc;

    COMMIT TRANSACTION;

    SELECT @Acquired          AS Acquired,
           @LeaseExpiresAtUtc AS LeaseExpiresAtUtc;
END;
