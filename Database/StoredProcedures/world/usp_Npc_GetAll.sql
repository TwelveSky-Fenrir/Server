CREATE PROCEDURE world.usp_Npc_GetAll
    AS
BEGIN
    SET
NOCOUNT ON;

SELECT NpcId,
       Name,
       Tribe,
       Type,
       DataSortNumber2D,
       DataSortNumber3D,
       Size1,
       Size2,
       Size3
FROM world.Npcs
ORDER BY NpcId;
END;
