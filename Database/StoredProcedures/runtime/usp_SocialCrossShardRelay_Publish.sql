-- Called once per outbound social-negotiation relay row (an Ask, from PartyInviteService/FriendService/
-- MentorAskService/DuelService/TradeInviteService/GuildInviteService after a same-shard ZoneRegistry miss
-- resolves the target via ICharacterShardLocationRepository; or an Answer, from whichever shard's own
-- SocialCrossShardRelayHost publishes a human or auto-declined response) -- never directly from an
-- IInlinePacketHandler's synchronous path. Single-row INSERT, no dependencies -- natively compiled like this
-- feature's sibling single-row hot-path procs (usp_GuildTribeBroadcastRelay_Publish,
-- usp_GameServer_Heartbeat, usp_CharacterShardLocation_Upsert).
CREATE PROCEDURE runtime.usp_SocialCrossShardRelay_Publish @Kind TINYINT,
                                                           @MessageType TINYINT,
                                                           @Accepted BIT NULL,
                                                           @ReasonCode TINYINT NULL,
                                                           @SourceShardId TINYINT,
                                                           @SourceCharacterId INT,
                                                           @SourceAvatarName NVARCHAR(13),
                                                           @TargetShardId TINYINT,
                                                           @TargetCharacterId INT,
                                                           @AskRelayId BIGINT NULL
    WITH NATIVE_COMPILATION , SCHEMABINDING
AS
BEGIN
    ATOMIC
    WITH (TRANSACTION ISOLATION LEVEL = SNAPSHOT, LANGUAGE = N'us_english')
    INSERT INTO runtime.SocialCrossShardRelay
    (Kind, MessageType, Accepted, ReasonCode, SourceShardId, SourceCharacterId, SourceAvatarName,
     TargetShardId, TargetCharacterId, AskRelayId, CreatedAtUtc)
    VALUES (@Kind, @MessageType, @Accepted, @ReasonCode, @SourceShardId, @SourceCharacterId, @SourceAvatarName,
            @TargetShardId, @TargetCharacterId, @AskRelayId, SYSUTCDATETIME());
END;
