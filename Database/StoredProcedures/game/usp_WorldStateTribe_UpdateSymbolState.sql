CREATE PROCEDURE game.usp_WorldStateTribe_UpdateSymbolState @TribeId TINYINT,
                                                            @SymbolDateUtc DATETIME2(3) = NULL,
                                                            @HasSymbol BIT,
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
        IsClosed           = @IsClosed,
        SymbolOwnerTribeId = @SymbolOwnerTribeId
    WHERE TribeId = @TribeId;
END;
