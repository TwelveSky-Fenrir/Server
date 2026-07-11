-- Called once per outbound cross-shard whisper row, from ChatCrossShardRelayHost's own outbound drain loop
-- (WhisperService.ResolveAsync enqueues after a same-shard ZoneRegistry miss resolves the target on another
-- shard via ICharacterShardLocationRepository) -- never directly from an IAsyncPacketHandler's per-connection
-- path. Single-row INSERT, no dependencies -- natively compiled like this feature's sibling single-row
-- hot-path procs (usp_SocialCrossShardRelay_Publish, usp_GuildTribeBroadcastRelay_Publish,
-- usp_CharacterShardLocation_Upsert).
CREATE PROCEDURE runtime.usp_ChatCrossShardRelay_Publish @SourceShardId TINYINT,
                                                         @SourceCharacterId INT,
                                                         @SourceAvatarName NVARCHAR(13),
                                                         @TargetShardId TINYINT,
                                                         @TargetCharacterId INT,
                                                         @TargetAvatarName NVARCHAR(13),
                                                         @Content NVARCHAR(61),
                                                         @SenderAuthType TINYINT
    WITH NATIVE_COMPILATION , SCHEMABINDING
AS
BEGIN
    ATOMIC
    WITH (TRANSACTION ISOLATION LEVEL = SNAPSHOT, LANGUAGE = N'us_english')
    INSERT INTO runtime.ChatCrossShardRelay
    (SourceShardId, SourceCharacterId, SourceAvatarName, TargetShardId, TargetCharacterId, TargetAvatarName,
     Content, SenderAuthType, CreatedAtUtc)
    VALUES (@SourceShardId, @SourceCharacterId, @SourceAvatarName, @TargetShardId, @TargetCharacterId,
            @TargetAvatarName, @Content, @SenderAuthType, SYSUTCDATETIME());
END;
