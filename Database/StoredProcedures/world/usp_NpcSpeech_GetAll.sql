CREATE PROCEDURE world.usp_NpcSpeech_GetAll
AS
BEGIN
    SET
        NOCOUNT ON;

    SELECT NpcId, SpeechGroup, SpeechIndex, Text
    FROM world.NpcSpeeches
    ORDER BY NpcId, SpeechGroup, SpeechIndex;
END;
