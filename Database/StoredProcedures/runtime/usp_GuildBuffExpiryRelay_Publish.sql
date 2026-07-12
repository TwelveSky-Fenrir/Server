-- Called once per edge-triggered guild-buff-reserve-exhaustion detection, from GuildBuffExpiryRelayHost's own
-- outbound drain loop -- never directly from Hosting.Guilds.GuildBuffDecayHost's own detection pass (see
-- IGuildBuffExpiryRelayQueue's own remarks for that boundary). Single-row INSERT, no dependencies -- natively
-- compiled like this feature's sibling single-row hot-path procs (usp_GuildTribeBroadcastRelay_Publish,
-- usp_GameServer_Heartbeat).
--
-- @CorrelationId retry-safe idempotency guard -- see usp_GuildTribeBroadcastRelay_Publish's own remarks for
-- the full rationale and why this uses the SELECT-into-variable/IS NULL shape rather than a bare IF EXISTS.
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
