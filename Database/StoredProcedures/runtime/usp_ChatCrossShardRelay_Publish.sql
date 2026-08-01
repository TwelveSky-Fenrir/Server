-- Called once per outbound cross-shard whisper row, from ChatCrossShardRelayHost's own outbound drain loop
-- (WhisperService.ResolveAsync enqueues after a same-shard ZoneRegistry miss resolves the target on another
-- shard via ICharacterShardLocationRepository) -- never directly from an IAsyncPacketHandler's per-connection
-- path. Single-row INSERT, no dependencies -- natively compiled like this feature's sibling single-row
-- hot-path procs (usp_SocialCrossShardRelay_Publish, usp_GuildTribeBroadcastRelay_Publish,
-- usp_CharacterShardLocation_Upsert).
--
-- @CorrelationId retry-safe idempotency guard -- see usp_GuildTribeBroadcastRelay_Publish's own remarks for
-- the full rationale and why this uses the SELECT-into-variable/IS NULL shape rather than a bare IF EXISTS.
CREATE PROCEDURE runtime.usp_ChatCrossShardRelay_Publish @SourceShardId TINYINT,
                                                         @SourceCharacterId INT,
                                                         @SourceAvatarName NVARCHAR(13),
                                                         @TargetShardId TINYINT,
                                                         @TargetCharacterId INT,
                                                         @TargetAvatarName NVARCHAR(13),
                                                         @Content NVARCHAR(61),
                                                         @SenderAuthType TINYINT,
                                                         @CorrelationId UNIQUEIDENTIFIER
    WITH NATIVE_COMPILATION , SCHEMABINDING
AS
BEGIN
    ATOMIC
    WITH (TRANSACTION ISOLATION LEVEL = SNAPSHOT, LANGUAGE = N'us_english')
    DECLARE @ExistingRelayId BIGINT = NULL;

    SELECT @ExistingRelayId = RelayId
    FROM runtime.ChatCrossShardRelay
    WHERE CorrelationId = @CorrelationId;

    IF @ExistingRelayId IS NULL
        INSERT INTO runtime.ChatCrossShardRelay
        (SourceShardId, SourceCharacterId, SourceAvatarName, TargetShardId, TargetCharacterId, TargetAvatarName,
         Content, SenderAuthType, CorrelationId, CreatedAtUtc)
        VALUES (@SourceShardId, @SourceCharacterId, @SourceAvatarName, @TargetShardId, @TargetCharacterId,
                @TargetAvatarName, @Content, @SenderAuthType, @CorrelationId, SYSUTCDATETIME());
END;
