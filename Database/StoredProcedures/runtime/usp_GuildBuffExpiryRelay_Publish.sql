-- Called once per edge-triggered guild-buff-reserve-exhaustion detection, from GuildBuffExpiryRelayHost's own
-- outbound drain loop -- never directly from Hosting.Guilds.GuildBuffDecayHost's own detection pass (see
-- IGuildBuffExpiryRelayQueue's own remarks for that boundary). Single-row INSERT, no dependencies -- natively
-- compiled like this feature's sibling single-row hot-path procs (usp_GuildTribeBroadcastRelay_Publish,
-- usp_GameServer_Heartbeat).
CREATE PROCEDURE runtime.usp_GuildBuffExpiryRelay_Publish @SourceShardId TINYINT,
                                                           @GuildId INT,
                                                           @NewBuffTime INT
    WITH NATIVE_COMPILATION , SCHEMABINDING
AS
BEGIN
    ATOMIC
    WITH (TRANSACTION ISOLATION LEVEL = SNAPSHOT, LANGUAGE = N'us_english')
    INSERT INTO runtime.GuildBuffExpiryRelay
    (SourceShardId, GuildId, NewBuffTime, CreatedAtUtc)
    VALUES (@SourceShardId, @GuildId, @NewBuffTime, SYSUTCDATETIME());
END;
