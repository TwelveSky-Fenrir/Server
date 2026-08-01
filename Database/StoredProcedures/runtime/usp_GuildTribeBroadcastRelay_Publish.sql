-- Called once per successfully-locally-delivered GuildAnnouncement/GuildChat/TribeAnnouncement/
-- TribeAnnouncementScroll broadcast, from GuildTribeBroadcastRelayHost's own outbound drain loop -- never
-- directly from an IInlinePacketHandler's synchronous path (see IGuildTribeBroadcastRelayQueue's own remarks
-- for that boundary). Single-row INSERT, no dependencies -- natively compiled like this feature's sibling
-- single-row hot-path procs (usp_GameServer_Heartbeat, usp_CharacterShardLocation_Upsert).
--
-- @CorrelationId is the retry-safe idempotency guard: CrossShardRelayRetry.RunAsync
-- (Application/Fenrir.Application.Game.Hosting/CrossShardRelayRetry.cs) retries this exact call up to twice on
-- ANY exception, including one raised after the INSERT below already committed but the client never saw the
-- acknowledgement -- without this check, that retry would insert a second, user-visible duplicate broadcast.
-- The lookup mirrors this file's own sibling procs' established "SELECT into a variable, branch on IS NULL"
-- shape (see usp_AccountSession_ClaimOrSignalKick/usp_CharacterShardLocation_Upsert) rather than a bare IF
-- EXISTS, so this stays consistent with constructs already proven to compile under NATIVE_COMPILATION in this
-- repo. runtime.GuildTribeBroadcastRelay's own UQ_..._CorrelationId constraint is the backstop against a
-- genuine concurrent double-publish racing this same lookup, not the primary dedup path.
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
                                                              @ItemLinkSocket2 INT NULL,
                                                              @CorrelationId UNIQUEIDENTIFIER
    WITH NATIVE_COMPILATION , SCHEMABINDING
AS
BEGIN
    ATOMIC
    WITH (TRANSACTION ISOLATION LEVEL = SNAPSHOT, LANGUAGE = N'us_english')
    DECLARE @ExistingRelayId BIGINT = NULL;

    SELECT @ExistingRelayId = RelayId
    FROM runtime.GuildTribeBroadcastRelay
    WHERE CorrelationId = @CorrelationId;

    IF @ExistingRelayId IS NULL
        INSERT INTO runtime.GuildTribeBroadcastRelay
        (Kind, SourceShardId, GuildId, Tribe, RoleField, AvatarName, Content, HasItemLink,
         ItemLinkIndex, ItemLinkActivity, ItemLinkValue, ItemLinkSocket0, ItemLinkSocket1, ItemLinkSocket2,
         CorrelationId, CreatedAtUtc)
        VALUES (@Kind, @SourceShardId, @GuildId, @Tribe, @RoleField, @AvatarName, @Content, @HasItemLink,
                @ItemLinkIndex, @ItemLinkActivity, @ItemLinkValue, @ItemLinkSocket0, @ItemLinkSocket1,
                @ItemLinkSocket2, @CorrelationId, SYSUTCDATETIME());
END;
