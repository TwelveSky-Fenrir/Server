CREATE PROCEDURE game.usp_WorldStateTribe_Update @TribeId TINYINT,
                                                 @SymbolDateUtc DATETIME2(3) = NULL,
                                                 @HasSymbol BIT,
                                                 @Points INT,
                                                 @IsClosed BIT,
                                                 @SymbolOwnerTribeId TINYINT
AS
BEGIN
    SET
        NOCOUNT ON;
    SET
        XACT_ABORT ON;

    UPDATE game.WorldStateTribes
    SET SymbolDateUtc      = @SymbolDateUtc,
        HasSymbol          = @HasSymbol,
        Points             = @Points,
        IsClosed           = @IsClosed,
        SymbolOwnerTribeId = @SymbolOwnerTribeId
    WHERE TribeId = @TribeId;
END;
