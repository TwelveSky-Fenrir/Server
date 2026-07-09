-- Called once per proxy-shop rental-extension consumable use, from ProxyShopExpirationRelayHost's own
-- outbound drain loop -- never directly from an IInlinePacketHandler's/*.Services synchronous path (see
-- IProxyShopExpirationRelayQueue's own remarks for that boundary). Single-row INSERT, no dependencies --
-- natively compiled like this feature's sibling single-row hot-path procs (usp_GuildTribeBroadcastRelay_Publish).
CREATE PROCEDURE runtime.usp_ProxyShopExpirationRelay_Publish @SourceShardId TINYINT,
                                                              @CharacterId INT,
                                                              @NewExpirationDate INT
    WITH NATIVE_COMPILATION , SCHEMABINDING
AS
BEGIN
    ATOMIC
    WITH (TRANSACTION ISOLATION LEVEL = SNAPSHOT, LANGUAGE = N'us_english')
    INSERT INTO runtime.ProxyShopExpirationRelay (SourceShardId, CharacterId, NewExpirationDate, CreatedAtUtc)
    VALUES (@SourceShardId, @CharacterId, @NewExpirationDate, SYSUTCDATETIME());
END;
