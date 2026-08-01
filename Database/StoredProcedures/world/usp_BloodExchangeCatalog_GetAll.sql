CREATE PROCEDURE world.usp_BloodExchangeCatalog_GetAll
AS
BEGIN
    SET
        NOCOUNT ON;

    SELECT BloodExchangeSlot, ItemId, Cost, Quantity
    FROM world.BloodExchangeCatalog
    ORDER BY BloodExchangeSlot;
END;
