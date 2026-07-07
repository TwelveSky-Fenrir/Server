CREATE PROCEDURE world.usp_QuestSpeech_GetAll
AS
BEGIN
    SET
        NOCOUNT ON;

    SELECT QuestId, SpeechKind, LineIndex, Text, Color
    FROM world.QuestSpeeches
    ORDER BY QuestId, SpeechKind, LineIndex;
END;
