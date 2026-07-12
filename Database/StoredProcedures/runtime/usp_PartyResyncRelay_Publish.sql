-- Called once per outbound party-resync row, from PartyResyncRelayHost's own outbound drain loop -- never
-- directly from an IAsyncPacketHandler's per-connection path (see IPartyResyncRelayQueue's own remarks for
-- that boundary). Single-row INSERT, no dependencies -- natively compiled like this feature family's sibling
-- single-row hot-path procs (usp_GuildTribeBroadcastRelay_Publish, usp_ChatCrossShardRelay_Publish,
-- usp_CharacterShardLocation_Upsert).
--
-- @CorrelationId retry-safe idempotency guard -- see usp_GuildTribeBroadcastRelay_Publish's own remarks for
-- the full rationale and why this uses the SELECT-into-variable/IS NULL shape rather than a bare IF EXISTS.
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
