CREATE PROCEDURE game.usp_WorldStateTribe_UpdateSymbolState @TribeId TINYINT,
                                                            @SymbolDateUtc DATETIME2(3) = NULL,
                                                            @HasSymbol BIT,
                                                            @IsClosed BIT,
                                                            @SymbolOwnerTribeId TINYINT,
                                                            @ExpectedWorldStateRevision BIGINT
AS
BEGIN
    SET
        NOCOUNT ON;
    SET
        XACT_ABORT ON;

    IF @ExpectedWorldStateRevision IS NULL OR @ExpectedWorldStateRevision < 0
        THROW 51202, N'A world-state revision must be nonnegative.', 1;

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
            SET SymbolDateUtc      = @SymbolDateUtc,
                HasSymbol          = @HasSymbol,
                IsClosed           = @IsClosed,
                SymbolOwnerTribeId = @SymbolOwnerTribeId
            WHERE TribeId = @TribeId;

            IF @@ROWCOUNT <> 1
                BEGIN
                    ROLLBACK TRANSACTION;
                    THROW 51203, N'A world-state tribe row is missing.', 1;
                END;

            SET @Applied = 1;
        END;

    COMMIT TRANSACTION;

    SELECT Applied = @Applied;
END;
