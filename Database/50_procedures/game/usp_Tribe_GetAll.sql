-- database/50_procedures/game/usp_Tribe_GetAll.sql
-- Contract: all 4 tribes (a genuinely small, fixed set -- MAX_TRIBE_NUM=4 -- so "all of them" is the
-- only realistic access pattern, unlike Guilds).
-- Params: none.
-- Result set (RS0), ordered by TribeId, always exactly 4 rows once seeded by the application:
--   1. TribeId           TINYINT (0-3)
--   2. MasterCharacterId INT NULL
--   3. Points            INT NULL -- lives on game.WorldStateTribes (the legacy WORLD_INFO home for this
--                                    field, see game.Tribes.sql's own header), NULL until
--                                    usp_WorldState_EnsureInitialized has created the singleton rows.
-- Idempotent: yes (read-only).
-- Errors: none.
CREATE PROCEDURE game.usp_Tribe_GetAll
    AS
BEGIN
    SET
NOCOUNT ON;

SELECT t.TribeId,
       t.MasterCharacterId,
       w.Points
FROM game.Tribes AS t
         LEFT JOIN game.WorldStateTribes AS w ON w.TribeId = t.TribeId
ORDER BY t.TribeId;
END;
