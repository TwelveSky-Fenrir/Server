CREATE PROCEDURE game.usp_TribeRole_GetForCharacter @CharacterId INT
AS
BEGIN
    SET NOCOUNT ON;

    SELECT TribeRole =
               CAST(
                   CASE
                       WHEN master.TribeId IS NOT NULL THEN 1
                       WHEN sub.CharacterId IS NOT NULL THEN 2
                       ELSE 0
                       END AS TINYINT)
    FROM game.Characters AS c
             LEFT JOIN game.Tribes AS master
                       ON master.TribeId = c.Tribe AND master.MasterCharacterId = c.CharacterId
             LEFT JOIN game.TribeSubMasters AS sub
                       ON sub.TribeId = c.Tribe AND sub.CharacterId = c.CharacterId
    WHERE c.CharacterId = @CharacterId;
END;
