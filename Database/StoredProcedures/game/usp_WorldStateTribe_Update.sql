-- database/50_procedures/game/usp_WorldStateTribe_Update.sql
-- Idempotent: a missing row (tribe never initialized) affects 0 rows and does not error.
CREATE PROCEDURE game.usp_WorldStateTribe_Update @TribeId TINYINT,
                                                 @SymbolDate DATETIME2(3) = NULL,
                                                 @HasSymbol BIT,
                                                 @Points INT,
                                                 @IsClosed BIT
AS
BEGIN
    SET
        NOCOUNT ON;
    SET
        XACT_ABORT ON;

    UPDATE game.WorldStateTribes
    SET SymbolDate = @SymbolDate,
        HasSymbol  = @HasSymbol,
        Points     = @Points,
        IsClosed   = @IsClosed
    WHERE TribeId = @TribeId;
END;
