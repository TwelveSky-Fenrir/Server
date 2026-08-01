-- Called once per outbound social-negotiation relay row (an Ask, from PartyInviteService/FriendService/
-- MentorAskService/DuelService/TradeInviteService/GuildInviteService after a same-shard ZoneRegistry miss
-- resolves the target via ICharacterShardLocationRepository; or an Answer, from whichever shard's own
-- SocialCrossShardRelayHost publishes a human or auto-declined response) -- never directly from an
-- IInlinePacketHandler's synchronous path. Single-row INSERT, no dependencies -- natively compiled like this
-- feature's sibling single-row hot-path procs (usp_GuildTribeBroadcastRelay_Publish,
-- usp_GameServer_Heartbeat, usp_CharacterShardLocation_Upsert).
--
-- @CorrelationId retry-safe idempotency guard -- see usp_GuildTribeBroadcastRelay_Publish's own remarks for
-- the full rationale and why this uses the SELECT-into-variable/IS NULL shape rather than a bare IF EXISTS.
CREATE PROCEDURE runtime.usp_SocialCrossShardRelay_Publish @Kind TINYINT,
                                                           @MessageType TINYINT,
                                                           @Accepted BIT NULL,
                                                           @ReasonCode TINYINT NULL,
                                                           @SourceShardId TINYINT,
                                                           @SourceCharacterId INT,
                                                           @SourceAvatarName NVARCHAR(13),
                                                           @TargetShardId TINYINT,
                                                           @TargetCharacterId INT,
                                                           @AskRelayId BIGINT NULL,
                                                           @CorrelationId UNIQUEIDENTIFIER
    WITH NATIVE_COMPILATION , SCHEMABINDING
AS
BEGIN
    ATOMIC
    WITH (TRANSACTION ISOLATION LEVEL = SNAPSHOT, LANGUAGE = N'us_english')
    DECLARE @ExistingRelayId BIGINT = NULL;

    SELECT @ExistingRelayId = RelayId
    FROM runtime.SocialCrossShardRelay
    WHERE CorrelationId = @CorrelationId;

    IF @ExistingRelayId IS NULL
        INSERT INTO runtime.SocialCrossShardRelay
        (Kind, MessageType, Accepted, ReasonCode, SourceShardId, SourceCharacterId, SourceAvatarName,
         TargetShardId, TargetCharacterId, AskRelayId, CorrelationId, CreatedAtUtc)
        VALUES (@Kind, @MessageType, @Accepted, @ReasonCode, @SourceShardId, @SourceCharacterId,
                @SourceAvatarName, @TargetShardId, @TargetCharacterId, @AskRelayId, @CorrelationId,
                SYSUTCDATETIME());
END;
