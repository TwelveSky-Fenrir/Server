CREATE PROCEDURE game.usp_WorldEventSnapshot_LoadAll
AS
BEGIN
    SET
        NOCOUNT ON;

    SELECT EventKind, OccurrenceKey, Revision, Phase, CanonicalPayload, CanonicalPayloadHash, UpdatedAtUtc
    FROM game.WorldEventSnapshots
    ORDER BY EventKind, OccurrenceKey;
END;
