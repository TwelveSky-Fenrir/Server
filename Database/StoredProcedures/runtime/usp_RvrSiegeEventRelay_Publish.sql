-- Called once per rvr-siege world event, from RvrSiegeEventRelayHost's own outbound drain loop -- never
-- directly from an IInlinePacketHandler's/*.Domain synchronous path (see IRvrSiegeEventRelayQueue's own
-- remarks for that boundary). Single-row INSERT, no dependencies -- natively compiled like this feature's
-- sibling single-row hot-path procs (usp_GuildTribeBroadcastRelay_Publish/usp_ProxyShopExpirationRelay_Publish).
CREATE PROCEDURE runtime.usp_RvrSiegeEventRelay_Publish @SourceShardId TINYINT,
                                                          @Sort INT,
                                                          @Data VARBINARY(130)
    WITH NATIVE_COMPILATION, SCHEMABINDING
AS
BEGIN
    ATOMIC
    WITH (TRANSACTION ISOLATION LEVEL = SNAPSHOT, LANGUAGE = N'us_english')
    INSERT INTO runtime.RvrSiegeEventRelay (SourceShardId, Sort, Data, CreatedAtUtc)
    VALUES (@SourceShardId, @Sort, @Data, SYSUTCDATETIME());
END;
