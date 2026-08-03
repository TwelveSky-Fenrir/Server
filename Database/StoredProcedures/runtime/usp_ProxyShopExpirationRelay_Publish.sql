CREATE PROCEDURE runtime.usp_ProxyShopExpirationRelay_Publish @SourceShardId TINYINT,
                                                              @CharacterId INT,
                                                              @NewExpirationDate INT,
                                                              @CorrelationId UNIQUEIDENTIFIER
    WITH NATIVE_COMPILATION , SCHEMABINDING
AS
BEGIN
    ATOMIC
    WITH (TRANSACTION ISOLATION LEVEL = SNAPSHOT, LANGUAGE = N'us_english')
    DECLARE @ExistingRelayId BIGINT = NULL;

    SELECT @ExistingRelayId = RelayId
    FROM runtime.ProxyShopExpirationRelay
    WHERE CorrelationId = @CorrelationId;

    IF @ExistingRelayId IS NULL
        INSERT INTO runtime.ProxyShopExpirationRelay
        (SourceShardId, CharacterId, NewExpirationDate, CorrelationId, CreatedAtUtc)
        VALUES (@SourceShardId, @CharacterId, @NewExpirationDate, @CorrelationId, SYSUTCDATETIME());
END;
