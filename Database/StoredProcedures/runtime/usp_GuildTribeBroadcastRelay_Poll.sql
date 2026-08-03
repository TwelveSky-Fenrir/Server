CREATE OR ALTER PROCEDURE runtime.usp_GuildTribeBroadcastRelay_Poll @ShardId TINYINT,
                                                                    @RetentionSeconds INT
AS
BEGIN
    SET
        NOCOUNT ON;
    SET
        XACT_ABORT ON;

    DECLARE @LastRelayId BIGINT = 0;
    DECLARE @NewLastRelayId BIGINT = NULL;

    SELECT @LastRelayId = LastRelayId
    FROM runtime.GuildTribeBroadcastCursor WITH (SNAPSHOT)
    WHERE ShardId = @ShardId;

    SELECT @NewLastRelayId = MAX(RelayId)
    FROM runtime.GuildTribeBroadcastRelay WITH (SNAPSHOT)
    WHERE RelayId > @LastRelayId
      AND SourceShardId <> @ShardId;

    SELECT RelayId,
           Kind,
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
           ItemLinkSocket2
    FROM runtime.GuildTribeBroadcastRelay WITH (SNAPSHOT)
    WHERE RelayId > @LastRelayId
      AND SourceShardId <> @ShardId
    ORDER BY RelayId ASC;

    IF @NewLastRelayId IS NOT NULL
        BEGIN
            UPDATE runtime.GuildTribeBroadcastCursor WITH (SNAPSHOT)
            SET LastRelayId = @NewLastRelayId
            WHERE ShardId = @ShardId;

            IF @@ROWCOUNT = 0
                INSERT INTO runtime.GuildTribeBroadcastCursor (ShardId, LastRelayId)
                VALUES (@ShardId, @NewLastRelayId);
        END;

    DELETE
    FROM runtime.GuildTribeBroadcastRelay WITH (SNAPSHOT)
    WHERE CreatedAtUtc <= DATEADD(SECOND, -@RetentionSeconds, SYSUTCDATETIME());
END;
