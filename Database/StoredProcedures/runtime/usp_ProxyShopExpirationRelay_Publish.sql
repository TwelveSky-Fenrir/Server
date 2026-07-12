-- Called once per proxy-shop rental-extension consumable use, from ProxyShopExpirationRelayHost's own
-- outbound drain loop -- never directly from an IInlinePacketHandler's/*.Services synchronous path (see
-- IProxyShopExpirationRelayQueue's own remarks for that boundary). Single-row INSERT, no dependencies --
-- natively compiled like this feature's sibling single-row hot-path procs (usp_GuildTribeBroadcastRelay_Publish).
--
-- @CorrelationId retry-safe idempotency guard -- see usp_GuildTribeBroadcastRelay_Publish's own remarks for
-- the full rationale and why this uses the SELECT-into-variable/IS NULL shape rather than a bare IF EXISTS.
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
