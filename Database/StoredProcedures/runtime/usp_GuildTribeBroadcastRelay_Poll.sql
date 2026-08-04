CREATE OR ALTER PROCEDURE runtime.usp_GuildTribeBroadcastRelay_Poll @ShardId TINYINT,
                                                                    @RetentionSeconds INT
AS
BEGIN
    SET
        NOCOUNT ON;
    SET
        XACT_ABORT ON;

    DECLARE @LastRelayId BIGINT = 0;
    SELECT @LastRelayId = LastRelayId
    FROM runtime.GuildTribeBroadcastCursor WITH (SNAPSHOT)
    WHERE ShardId = @ShardId;

    SELECT RelayId,
           Kind,
           SourceShardId,
           SourceCharacterId,
           SystemCause,
           GuildId,
           Tribe,
           RoleField,
           AvatarName,
           Content,
           HasItemLink,
           ItemLinkIndex,
           ItemLinkActivity,
           ItemLinkValue,
           ItemLinkSocket0,
           ItemLinkSocket1,
           ItemLinkSocket2,
           CorrelationId
    FROM runtime.GuildTribeBroadcastRelay WITH (SNAPSHOT)
    WHERE RelayId > @LastRelayId
      AND SourceShardId <> @ShardId
    ORDER BY RelayId ASC;

END;
