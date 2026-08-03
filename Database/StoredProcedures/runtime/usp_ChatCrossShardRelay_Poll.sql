CREATE OR ALTER PROCEDURE runtime.usp_ChatCrossShardRelay_Poll @ShardId TINYINT,
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
    FROM runtime.ChatCrossShardRelayCursor WITH (SNAPSHOT)
    WHERE ShardId = @ShardId;

    SELECT @NewLastRelayId = MAX(RelayId)
    FROM runtime.ChatCrossShardRelay WITH (SNAPSHOT)
    WHERE RelayId > @LastRelayId
      AND TargetShardId = @ShardId;

    SELECT RelayId,
           SourceShardId,
           SourceCharacterId,
           SourceAvatarName,
           TargetShardId,
           TargetCharacterId,
           TargetAvatarName,
           Content,
           SenderAuthType
    FROM runtime.ChatCrossShardRelay WITH (SNAPSHOT)
    WHERE RelayId > @LastRelayId
      AND TargetShardId = @ShardId
    ORDER BY RelayId ASC;

    IF @NewLastRelayId IS NOT NULL
        BEGIN
            UPDATE runtime.ChatCrossShardRelayCursor WITH (SNAPSHOT)
            SET LastRelayId = @NewLastRelayId
            WHERE ShardId = @ShardId;

            IF @@ROWCOUNT = 0
                INSERT INTO runtime.ChatCrossShardRelayCursor (ShardId, LastRelayId)
                VALUES (@ShardId, @NewLastRelayId);
        END;

    DELETE
    FROM runtime.ChatCrossShardRelay WITH (SNAPSHOT)
    WHERE CreatedAtUtc <= DATEADD(SECOND, -@RetentionSeconds, SYSUTCDATETIME());
END;
