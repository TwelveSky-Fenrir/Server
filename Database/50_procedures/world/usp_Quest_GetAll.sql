-- Contract: no parameters -> RS0, one row per world.Quests row (688 rows). Called once at GameServer boot to
-- populate an in-memory cache (e.g. FrozenDictionary<int, Quest> keyed by QuestId) -- world.* is read-mostly
-- reference data, never queried per-game-tick, so a plain full-table SELECT is correct here, not a per-request
-- lookup proc. world.usp_QuestReward_GetAll / world.usp_QuestSpeech_GetAll are separate calls (not JOINed in
-- here): both are fan-out child tables, so joining either into this one-row-per-quest result would multiply
-- the base row per reward slot / speech line.
-- Ordered by QuestId (the legacy qIndex) for deterministic, cache-friendly load order.
-- Idempotent: yes (read-only). Errors: none.
CREATE PROCEDURE world.usp_Quest_GetAll
    AS
BEGIN
    SET
NOCOUNT ON;

SELECT QuestId,
       Subject,
       Category,
       Step,
       Level,
       Type,
       Sort,
       SummonZoneNumber,
       SummonPosX,
       SummonPosY,
       SummonPosZ,
       StartNPCNumber,
       KeyNpcNumber1,
       KeyNpcNumber2,
       KeyNpcNumber3,
       KeyNpcNumber4,
       KeyNpcNumber5,
       EndNPCNumber,
       Solution1,
       Solution2,
       Solution3,
       Solution4,
       NextIndex
FROM world.Quests
ORDER BY QuestId;
END;
