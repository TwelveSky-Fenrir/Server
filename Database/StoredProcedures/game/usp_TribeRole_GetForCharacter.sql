-- database/50_procedures/game/usp_TribeRole_GetForCharacter.sql
-- Mirrors legacy ReturnTribeRole (function.h:92-114): 1=master, 2=sub-master, 0=regular member. The
-- vote-candidate outcome (3) is a separate feature, deliberately not resolved here.
CREATE PROCEDURE game.usp_TribeRole_GetForCharacter @CharacterId INT
AS
BEGIN
    SET
        NOCOUNT ON;

    SELECT TribeRole =
               CASE
                   WHEN master.TribeId IS NOT NULL THEN 1
                   WHEN sub.CharacterId IS NOT NULL THEN 2
                   ELSE 0
                   END
    FROM game.Characters AS c
             LEFT JOIN game.Tribes AS master
                       ON master.TribeId = c.Tribe AND master.MasterCharacterId = c.CharacterId
             LEFT JOIN game.TribeSubMasters AS sub
                       ON sub.TribeId = c.Tribe AND sub.CharacterId = c.CharacterId
    WHERE c.CharacterId = @CharacterId;
END;
