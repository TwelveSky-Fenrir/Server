-- Called once per rvr-siege world event, from RvrSiegeEventRelayHost's own outbound drain loop -- never
-- directly from an IInlinePacketHandler's/*.Domain synchronous path (see IRvrSiegeEventRelayQueue's own
-- remarks for that boundary). Single-row INSERT, no dependencies -- natively compiled like this feature's
-- sibling single-row hot-path procs (usp_GuildTribeBroadcastRelay_Publish/usp_ProxyShopExpirationRelay_Publish).
--
-- @CorrelationId retry-safe idempotency guard -- see usp_GuildTribeBroadcastRelay_Publish's own remarks for
-- the full rationale and why this uses the SELECT-into-variable/IS NULL shape rather than a bare IF EXISTS.
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
