CREATE PROCEDURE runtime.usp_PartyResyncRelay_Publish @Sort TINYINT,
                                                      @SourceShardId TINYINT,
                                                      @SourceCharacterId INT,
                                                      @PartyName NVARCHAR(13),
                                                      @AvatarName NVARCHAR(13),
                                                      @CorrelationId UNIQUEIDENTIFIER
    WITH NATIVE_COMPILATION , SCHEMABINDING
AS
BEGIN
    ATOMIC
    WITH (TRANSACTION ISOLATION LEVEL = SNAPSHOT, LANGUAGE = N'us_english')
    DECLARE @ExistingRelayId BIGINT = NULL;

    SELECT @ExistingRelayId = RelayId
    FROM runtime.PartyResyncRelay
    WHERE CorrelationId = @CorrelationId;

    IF @ExistingRelayId IS NULL
        INSERT INTO runtime.PartyResyncRelay
        (Sort, SourceShardId, SourceCharacterId, PartyName, AvatarName, CorrelationId, CreatedAtUtc)
        VALUES (@Sort, @SourceShardId, @SourceCharacterId, @PartyName, @AvatarName, @CorrelationId,
                SYSUTCDATETIME());
END;
