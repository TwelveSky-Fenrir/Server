-- database/50_procedures/game/usp_TribeRole_GetForCharacter.sql
-- Contract: this character's tribe role, mirroring the legacy ReturnTribeRole(TRIBE_INFO*, name, tribe)
-- (verified in full, Server/Header/function.h:92-114): 1 = tribe master (matches game.Tribes.MasterCharacterId),
-- 2 = sub-master (matches a game.TribeSubMasters row), 0 = regular member. ReturnTribeRole's own third
-- outcome (3 = vote-candidate, game.TribeVotes) is a DIFFERENT feature (tribe leadership elections) not
-- consumed by this lot's chat/announcement gating -- deliberately not resolved here.
-- Params:
--   @CharacterId INT
-- Result set (RS0), 0 or 1 row (0 only for an unknown @CharacterId, which cannot happen for a caller
-- resolving a world-entry-verified character -- treat "no row" as role 0, same as an explicit 0):
--   1. TribeRole TINYINT (0, 1, or 2 -- see above)
-- Idempotent: yes (read-only).
-- Errors: none.
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
