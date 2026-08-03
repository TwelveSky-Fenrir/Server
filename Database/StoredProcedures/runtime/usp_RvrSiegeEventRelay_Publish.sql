CREATE PROCEDURE runtime.usp_RvrSiegeEventRelay_Publish @SourceShardId TINYINT,
                                                        @Sort INT,
                                                        @Data VARBINARY(130),
                                                        @CorrelationId UNIQUEIDENTIFIER
    WITH NATIVE_COMPILATION , SCHEMABINDING
AS
BEGIN
    ATOMIC
    WITH (TRANSACTION ISOLATION LEVEL = SNAPSHOT, LANGUAGE = N'us_english')
    DECLARE @ExistingRelayId BIGINT = NULL;

    SELECT @ExistingRelayId = RelayId
    FROM runtime.RvrSiegeEventRelay
    WHERE CorrelationId = @CorrelationId;

    IF @ExistingRelayId IS NULL
        INSERT INTO runtime.RvrSiegeEventRelay (SourceShardId, Sort, Data, CorrelationId, CreatedAtUtc)
        VALUES (@SourceShardId, @Sort, @Data, @CorrelationId, SYSUTCDATETIME());
END;
