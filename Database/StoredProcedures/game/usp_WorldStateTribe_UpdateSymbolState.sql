-- database/50_procedures/game/usp_WorldStateTribe_UpdateSymbolState.sql
-- Same whole-row overwrite as usp_WorldStateTribe_Update, minus Points: SymbolDate/HasSymbol/IsClosed only
-- ever have one writer cluster-wide at a time (every production caller is ZoneEventBroadcaster, acting on
-- behalf of one specific zone -- ADR-0012 rule 1, a map is hosted by exactly one shard), so a plain
-- overwrite is safe for these fields. Points is deliberately excluded here -- see
-- usp_WorldStateTribe_AddPoints for the concurrently-safe delta path RvR kill-scoring needs instead.
-- Idempotent-on-missing-row like usp_WorldStateTribe_Update: a missing row affects 0 rows and does not error.
CREATE PROCEDURE game.usp_WorldStateTribe_UpdateSymbolState @TribeId    TINYINT,
    @SymbolDate DATETIME2(3) = NULL,
    @HasSymbol  BIT,
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
    IsClosed   = @IsClosed
WHERE TribeId = @TribeId;
END;
