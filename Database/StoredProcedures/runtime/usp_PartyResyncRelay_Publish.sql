-- Called once per outbound party-resync row, from PartyResyncRelayHost's own outbound drain loop -- never
-- directly from an IAsyncPacketHandler's per-connection path (see IPartyResyncRelayQueue's own remarks for
-- that boundary). Single-row INSERT, no dependencies -- natively compiled like this feature family's sibling
-- single-row hot-path procs (usp_GuildTribeBroadcastRelay_Publish, usp_ChatCrossShardRelay_Publish,
-- usp_CharacterShardLocation_Upsert).
CREATE PROCEDURE runtime.usp_PartyResyncRelay_Publish @Sort TINYINT,
                                                      @SourceShardId TINYINT,
                                                      @SourceCharacterId INT,
                                                      @PartyName NVARCHAR(13),
                                                      @AvatarName NVARCHAR(13)
    WITH NATIVE_COMPILATION , SCHEMABINDING
AS
BEGIN
    ATOMIC
    WITH (TRANSACTION ISOLATION LEVEL = SNAPSHOT, LANGUAGE = N'us_english')
    INSERT INTO runtime.PartyResyncRelay
    (Sort, SourceShardId, SourceCharacterId, PartyName, AvatarName, CreatedAtUtc)
    VALUES (@Sort, @SourceShardId, @SourceCharacterId, @PartyName, @AvatarName, SYSUTCDATETIME());
END;
