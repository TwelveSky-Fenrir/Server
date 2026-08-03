CREATE OR ALTER PROCEDURE runtime.usp_SocialCrossShardRelay_Poll @ShardId TINYINT,
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
    FROM runtime.SocialCrossShardRelayCursor WITH (SNAPSHOT)
    WHERE ShardId = @ShardId;

    SELECT @NewLastRelayId = MAX(RelayId)
    FROM runtime.SocialCrossShardRelay WITH (SNAPSHOT)
    WHERE RelayId > @LastRelayId
      AND TargetShardId = @ShardId;

    SELECT RelayId,
           Kind,
           MessageType,
           Accepted,
           ReasonCode,
           SourceShardId,
           SourceCharacterId,
           SourceAvatarName,
           TargetShardId,
           TargetCharacterId,
           AskRelayId
    FROM runtime.SocialCrossShardRelay WITH (SNAPSHOT)
    WHERE RelayId > @LastRelayId
      AND TargetShardId = @ShardId
    ORDER BY RelayId ASC;

    IF @NewLastRelayId IS NOT NULL
        BEGIN
            UPDATE runtime.SocialCrossShardRelayCursor WITH (SNAPSHOT)
            SET LastRelayId = @NewLastRelayId
            WHERE ShardId = @ShardId;

            IF @@ROWCOUNT = 0
                INSERT INTO runtime.SocialCrossShardRelayCursor (ShardId, LastRelayId)
                VALUES (@ShardId, @NewLastRelayId);
        END;

    DELETE
    FROM runtime.SocialCrossShardRelay WITH (SNAPSHOT)
    WHERE CreatedAtUtc <= DATEADD(SECOND, -@RetentionSeconds, SYSUTCDATETIME());
END;
