-- Called once per successfully-locally-delivered GuildAnnouncement/GuildChat/TribeAnnouncement/
-- TribeAnnouncementScroll broadcast, from GuildTribeBroadcastRelayHost's own outbound drain loop -- never
-- directly from an IInlinePacketHandler's synchronous path (see IGuildTribeBroadcastRelayQueue's own remarks
-- for that boundary). Single-row INSERT, no dependencies -- natively compiled like this feature's sibling
-- single-row hot-path procs (usp_GameServer_Heartbeat, usp_CharacterShardLocation_Upsert).
CREATE PROCEDURE runtime.usp_GuildTribeBroadcastRelay_Publish @Kind TINYINT,
                                                               @SourceShardId TINYINT,
                                                               @GuildId INT NULL,
                                                               @Tribe TINYINT NULL,
                                                               @RoleField TINYINT,
                                                               @AvatarName NVARCHAR(13),
                                                               @Content NVARCHAR(61),
                                                               @HasItemLink BIT,
                                                               @ItemLinkIndex INT NULL,
                                                               @ItemLinkActivity INT NULL,
                                                               @ItemLinkValue INT NULL,
                                                               @ItemLinkSocket0 INT NULL,
                                                               @ItemLinkSocket1 INT NULL,
                                                               @ItemLinkSocket2 INT NULL
    WITH NATIVE_COMPILATION, SCHEMABINDING
AS
BEGIN
    ATOMIC
    WITH (TRANSACTION ISOLATION LEVEL = SNAPSHOT, LANGUAGE = N'us_english')
    INSERT INTO runtime.GuildTribeBroadcastRelay
    (Kind, SourceShardId, GuildId, Tribe, RoleField, AvatarName, Content, HasItemLink,
     ItemLinkIndex, ItemLinkActivity, ItemLinkValue, ItemLinkSocket0, ItemLinkSocket1, ItemLinkSocket2,
     CreatedAtUtc)
    VALUES (@Kind, @SourceShardId, @GuildId, @Tribe, @RoleField, @AvatarName, @Content, @HasItemLink,
            @ItemLinkIndex, @ItemLinkActivity, @ItemLinkValue, @ItemLinkSocket0, @ItemLinkSocket1,
            @ItemLinkSocket2, SYSUTCDATETIME());
END;
