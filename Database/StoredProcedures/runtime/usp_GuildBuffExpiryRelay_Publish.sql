CREATE PROCEDURE runtime.usp_GuildBuffExpiryRelay_Publish @SourceShardId TINYINT,
                                                          @GuildId INT,
                                                          @NewBuffTime INT,
                                                          @CorrelationId UNIQUEIDENTIFIER
    WITH NATIVE_COMPILATION , SCHEMABINDING
AS
BEGIN
    ATOMIC
    WITH (TRANSACTION ISOLATION LEVEL = SNAPSHOT, LANGUAGE = N'us_english')
    DECLARE @ExistingRelayId BIGINT = NULL;

    SELECT @ExistingRelayId = RelayId
    FROM runtime.GuildBuffExpiryRelay
    WHERE CorrelationId = @CorrelationId;

    IF @ExistingRelayId IS NULL
        INSERT INTO runtime.GuildBuffExpiryRelay
            (SourceShardId, GuildId, NewBuffTime, CorrelationId, CreatedAtUtc)
        VALUES (@SourceShardId, @GuildId, @NewBuffTime, @CorrelationId, SYSUTCDATETIME());
END;
