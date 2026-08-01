CREATE PROCEDURE world.usp_NpcMenuOption_GetAll
AS
BEGIN
    SET
        NOCOUNT ON;

    SELECT NpcId, SlotIndex, OptionId
    FROM world.NpcMenuOptions
    ORDER BY NpcId, SlotIndex;
END;
