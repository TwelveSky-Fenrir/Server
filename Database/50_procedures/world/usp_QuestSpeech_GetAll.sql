-- Contract: no parameters -> RS0, one row per world.QuestSpeeches row (18,742 rows -- only the non-empty
-- dialogue lines, see that table's header comment for how much the sparse per-line normalization saved versus
-- the theoretical 150,000 max). Called once at GameServer boot alongside world.usp_Quest_GetAll to build a
-- per-QuestId speech-line lookup (e.g. grouped by SpeechKind client-side).
-- Ordered by QuestId, SpeechKind, LineIndex for deterministic, cache-friendly load order.
-- Idempotent: yes (read-only). Errors: none.
CREATE PROCEDURE world.usp_QuestSpeech_GetAll
    AS
BEGIN
    SET
NOCOUNT ON;

SELECT QuestId, SpeechKind, LineIndex, Text, Color
FROM world.QuestSpeeches
ORDER BY QuestId, SpeechKind, LineIndex;
END;
