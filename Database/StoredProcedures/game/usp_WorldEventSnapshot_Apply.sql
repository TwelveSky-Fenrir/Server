CREATE PROCEDURE game.usp_WorldEventSnapshot_Apply @EventKind VARCHAR(48),
                                                    @OccurrenceKey VARCHAR(96),
                                                    @ExpectedRevision BIGINT,
                                                    @Phase VARCHAR(48),
                                                    @CanonicalPayload NVARCHAR(MAX),
                                                    @CanonicalPayloadHash BINARY(32)
AS
BEGIN
    SET
        NOCOUNT ON;
    SET
        XACT_ABORT ON;

    IF @EventKind = ''
        THROW 50711, N'A world-event snapshot kind is required.', 1;

    IF @OccurrenceKey = ''
        THROW 50712, N'A world-event snapshot occurrence key is required.', 1;

    IF @ExpectedRevision < 0
        THROW 50713, N'A world-event snapshot revision cannot be negative.', 1;

    IF @Phase = ''
        THROW 50714, N'A world-event snapshot phase is required.', 1;

    DECLARE @CurrentRevision BIGINT;
    DECLARE @Applied BIT = 0;

    BEGIN TRANSACTION;

    SELECT @CurrentRevision = Revision
    FROM game.WorldEventSnapshots WITH (UPDLOCK, HOLDLOCK)
    WHERE EventKind = @EventKind
      AND OccurrenceKey = @OccurrenceKey;

    IF @CurrentRevision IS NULL
        BEGIN
            IF @ExpectedRevision = 0
                BEGIN
                    INSERT INTO game.WorldEventSnapshots
                        (EventKind, OccurrenceKey, Revision, Phase, CanonicalPayload, CanonicalPayloadHash)
                    VALUES (@EventKind, @OccurrenceKey, 1, @Phase, @CanonicalPayload, @CanonicalPayloadHash);

                    SET @Applied = 1;
                END;
        END
    ELSE IF @CurrentRevision = @ExpectedRevision
        BEGIN
            UPDATE game.WorldEventSnapshots
            SET Revision             = Revision + 1,
                Phase                = @Phase,
                CanonicalPayload     = @CanonicalPayload,
                CanonicalPayloadHash = @CanonicalPayloadHash,
                UpdatedAtUtc         = SYSUTCDATETIME()
            WHERE EventKind = @EventKind
              AND OccurrenceKey = @OccurrenceKey;

            SET @Applied = 1;
        END;

    COMMIT TRANSACTION;

    SELECT Applied = @Applied;
END;
