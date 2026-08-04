CREATE PROCEDURE game.usp_WorldStateAllianceOffer_Set @FromTribeId TINYINT,
                                                      @ToTribeId TINYINT,
                                                      @IsAccepted BIT,
                                                      @ExpectedWorldStateRevision BIGINT
AS
BEGIN
    SET
        NOCOUNT ON;
    SET
        XACT_ABORT ON;

    IF @ExpectedWorldStateRevision IS NULL OR @ExpectedWorldStateRevision < 0
        THROW 51206, N'A world-state revision must be nonnegative.', 1;

    DECLARE @Applied BIT = 0;

    BEGIN TRANSACTION;

    UPDATE game.WorldState
    SET Revision     = Revision + 1,
        UpdatedAtUtc = SYSUTCDATETIME()
    WHERE Id = 1
      AND Revision = @ExpectedWorldStateRevision;

    IF @@ROWCOUNT = 1
        BEGIN
            UPDATE game.WorldStateAllianceOffers
            SET IsAccepted = @IsAccepted
            WHERE FromTribeId = @FromTribeId
              AND ToTribeId = @ToTribeId;

            IF @@ROWCOUNT = 0
                INSERT INTO game.WorldStateAllianceOffers (FromTribeId, ToTribeId, IsAccepted)
                VALUES (@FromTribeId, @ToTribeId, @IsAccepted);

            SET @Applied = 1;
        END;

    COMMIT TRANSACTION;

    SELECT Applied = @Applied;
END;
