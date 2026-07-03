-- database/50_procedures/game/usp_WorldStateTribe_Update.sql
-- Contract: update one tribe's symbol/points/close-state row.
-- Params:
--   @TribeId     TINYINT      -- game.Tribes.TribeId (0-3)
--   @SymbolDate  DATETIME2(3) NULL
--   @HasSymbol   BIT          -- see game.WorldStateTribes for the "still holds its own symbol" semantics
--   @Points      INT
--   @IsClosed    BIT
-- Result set: none.
-- Idempotent: yes -- a missing row (game.usp_WorldState_EnsureInitialized never called for this TribeId)
-- affects 0 rows, same idempotent-no-error idiom as game.usp_Character_Delete.
CREATE PROCEDURE game.usp_WorldStateTribe_Update @TribeId    TINYINT,
    @SymbolDate DATETIME2(3) = NULL,
    @HasSymbol  BIT,
    @Points     INT,
    @IsClosed   BIT
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
