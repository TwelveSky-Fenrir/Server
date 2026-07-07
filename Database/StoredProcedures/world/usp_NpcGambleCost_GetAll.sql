CREATE PROCEDURE world.usp_NpcGambleCost_GetAll
AS
BEGIN
    SET
        NOCOUNT ON;

    SELECT NpcId, GambleTier, CostIndex, Value
    FROM world.NpcGambleCosts
    ORDER BY NpcId, GambleTier, CostIndex;
END;
