-- Contract: no parameters -> RS0 rows { BloodExchangeSlot INT, ItemId INT NULL, Cost INT,
--           Quantity INT }, every row -- the boot-time full-table load for GameServer's in-memory
-- blood-exchange cache (only 3 real rows exist; see 30_tables/world/BloodExchangeCatalog.sql).
-- Read-only, safe to retry.
CREATE PROCEDURE world.usp_BloodExchangeCatalog_GetAll
    AS
BEGIN
    SET
NOCOUNT ON;

SELECT BloodExchangeSlot, ItemId, Cost, Quantity
FROM world.BloodExchangeCatalog
ORDER BY BloodExchangeSlot;
END;
