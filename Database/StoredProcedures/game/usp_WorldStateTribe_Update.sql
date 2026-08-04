CREATE PROCEDURE game.usp_WorldStateTribe_Update @TribeId TINYINT,
                                                 @Points INT,
                                                 @ExpectedWorldStateRevision BIGINT
AS
BEGIN
    SET
        NOCOUNT ON;
    SET
        XACT_ABORT ON;

    IF @ExpectedWorldStateRevision IS NULL OR @ExpectedWorldStateRevision < 0
        THROW 51200, N'A world-state revision must be nonnegative.', 1;

    DECLARE @Applied BIT = 0;

    BEGIN TRANSACTION;

    UPDATE game.WorldState
    SET Revision     = Revision + 1,
        UpdatedAtUtc = SYSUTCDATETIME()
    WHERE Id = 1
      AND Revision = @ExpectedWorldStateRevision;

    IF @@ROWCOUNT = 1
        BEGIN
            UPDATE game.WorldStateTribes
            SET Points = @Points
            WHERE TribeId = @TribeId;

            IF @@ROWCOUNT <> 1
                BEGIN
                    ROLLBACK TRANSACTION;
                    THROW 51201, N'A world-state tribe row is missing.', 1;
                END;

            SET @Applied = 1;
        END;

    COMMIT TRANSACTION;

    SELECT Applied = @Applied;
END;
