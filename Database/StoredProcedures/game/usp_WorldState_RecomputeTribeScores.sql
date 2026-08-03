CREATE PROCEDURE game.usp_WorldState_RecomputeTribeScores
AS
BEGIN
    SET
        NOCOUNT ON;

    SELECT c.Tribe                                                                      AS TribeId,
           SUM(CAST((c.Level - 112) + (c.Level2 * 3) + (c.RebirthCount * 3) AS BIGINT)) AS StatSum
    FROM game.Characters c
    WHERE c.Level >= 145
    GROUP BY c.Tribe;
END;
